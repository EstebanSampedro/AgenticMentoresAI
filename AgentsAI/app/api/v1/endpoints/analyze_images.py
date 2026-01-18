# app/api/v1/endpoints/analyze_images.py

from app.utils.response      import success_response
from fastapi import APIRouter, HTTPException, status, File, UploadFile, Form, Depends
from app.services.azure_openai_client import azure_client
from datetime import datetime
from app.utils.session_store import get_uploaded_docs, add_uploaded_doc, set_ocr_result
from app.schemas.analyze_images import ImageAnalysisResponse
from app.core.config import settings
import logging
import base64
import io
import json
import re
import unicodedata
from app.core.security import get_token_payload

try:
    from PyPDF2 import PdfReader
except ImportError:
    try:
        from pypdf import PdfReader
    except ImportError:
        PdfReader = None

try:
    import fitz  # PyMuPDF para extraer imágenes de PDFs
except ImportError:
    fitz = None

logger = logging.getLogger(__name__)
router = APIRouter(
    dependencies=[Depends(get_token_payload)]
)

# ------------------------------------------------------------------------------------
# Helpers para normalizar fechas (soporta '11 de abril del 2025' y formatos numéricos)
# ------------------------------------------------------------------------------------
_SP_MONTHS = {
    "enero": 1, "ene": 1,
    "febrero": 2, "feb": 2,
    "marzo": 3, "mar": 3,
    "abril": 4, "abr": 4,
    "mayo": 5, "may": 5,
    "junio": 6, "jun": 6,
    "julio": 7, "jul": 7,
    "agosto": 8, "ago": 8,
    "septiembre": 9, "setiembre": 9, "sept": 9, "sep": 9, "set": 9,
    "octubre": 10, "oct": 10,
    "noviembre": 11, "nov": 11,
    "diciembre": 12, "dic": 12,
}

def _remove_accents(s: str) -> str:
    """Remove accents from string"""
    return "".join(ch for ch in unicodedata.normalize("NFD", s) if unicodedata.category(ch) != "Mn")

def _to_iso_numeric(d: str) -> str:
    """
    Normaliza formatos numéricos a YYYY-MM-DD.
    Acepta:
      - yyyy-mm-dd / yyyy/mm/dd / yyyy.mm.dd
      - dd-mm-yyyy / dd/mm/yyyy / dd.mm.yyyy
      - dd-mm-yy / dd/mm/yy  -> 20xx/19xx heurístico
    """
    d = (d or "").strip()
    if not d:
        return ""
    # yyyy-mm-dd
    m = re.match(r"^(\d{4})[\/\-.](\d{1,2})[\/\-.](\d{1,2})$", d)
    if m:
        y, mth, day = map(int, m.groups())
    else:
        # dd/mm/yyyy o dd-mm-yyyy (o yy)
        m = re.match(r"^(\d{1,2})[\/\-.](\d{1,2})[\/\-.](\d{2,4})$", d)
        if not m:
            return ""
        day, mth, y = m.groups()
        y, mth, day = int(y), int(mth), int(day)
        if y < 100:
            y += 2000 if y < 50 else 1900
    try:
        from datetime import date as _date
        _date(y, mth, day)
        return f"{y:04d}-{mth:02d}-{day:02d}"
    except Exception:
        return ""

_SPANISH_TEXTUAL_DATE_RE = re.compile(
    r"\b(\d{1,2})\s+de\s+([a-záéíóúñ\.]+)\s+(?:de|del)\s+(\d{4})\b",
    flags=re.IGNORECASE
)

def _spanish_text_to_iso(text: str) -> str:
    """
    Extrae la primera fecha con formato español textual del string y la devuelve YYYY-MM-DD.
    Ej: '11 de abril del 2025', '7 de sept de 2024', '03 de dic de 2023'
    """
    for m in _SPANISH_TEXTUAL_DATE_RE.finditer(text or ""):
        day = int(m.group(1))
        month_txt = _remove_accents(m.group(2).rstrip(".").lower())
        year = int(m.group(3))
        month = _SP_MONTHS.get(month_txt)
        if not month:
            continue
        try:
            from datetime import date as _date
            _date(year, month, day)
            return f"{year:04d}-{month:02d}-{day:02d}"
        except Exception:
            continue
    return ""

def _normalize_any_date(s: str) -> str:
    """Intenta normalizar 's' a YYYY-MM-DD. Soporta numéricas y español textual."""
    return _to_iso_numeric(s) or _spanish_text_to_iso(s)

def _find_dates_in_text(analysis: str) -> list[str]:
    """
    Devuelve fechas en orden de aparición a partir de un texto.
    Busca tanto numéricas como textual-español.
    """
    results, seen = [], set()

    # Numéricas
    for m in re.finditer(r"\b\d{4}[\/\-.]\d{1,2}[\/\-.]\d{1,2}\b", analysis):
        iso = _to_iso_numeric(m.group(0))
        if iso and iso not in seen:
            seen.add(iso); results.append(iso)
    for m in re.finditer(r"\b\d{1,2}[\/\-.]\d{1,2}[\/\-.]\d{2,4}\b", analysis):
        iso = _to_iso_numeric(m.group(0))
        if iso and iso not in seen:
            seen.add(iso); results.append(iso)

    # Español textual
    for m in _SPANISH_TEXTUAL_DATE_RE.finditer(analysis):
        day = int(m.group(1))
        month_txt = _remove_accents(m.group(2).rstrip(".").lower())
        year = int(m.group(3))
        month = _SP_MONTHS.get(month_txt)
        if not month:
            continue
        try:
            from datetime import date as _date
            _date(year, month, day)
            iso = f"{year:04d}-{month:02d}-{day:02d}"
            if iso not in seen:
                seen.add(iso); results.append(iso)
        except Exception:
            continue

    return results

# ------------------------------------------------------------------------------------

@router.post(
    "/analyze-file/",
    response_model=ImageAnalysisResponse,
    summary="Analiza imágenes y documentos PDF para certificados médicos y deportivos"
)
async def analyze_image_file(
    session_id: str = Form(..., description="ID de la sesión para agrupar documentos"),
    image_file: UploadFile = File(
        ..., 
        description="Imagen (PNG/JPEG/GIF) o documento PDF hasta 10 MB"
    )
):
    """
    Analiza archivos de imagen o PDF para identificar y validar certificados médicos,
    deportivos, actas de defunción y otros documentos oficiales.
    
    **SEGURIDAD**: session_id se pasa en el body (Form) para evitar exposición en URLs/logs.
    """
    # --- 1) validaciones ---
    content = await image_file.read()
    if len(content) > 10 * 1024 * 1024:
        raise HTTPException(
            status_code=status.HTTP_413_REQUEST_ENTITY_TOO_LARGE,
            detail="El archivo excede 10 MB"
        )
    allowed_types = [
        "image/png", "image/jpeg", "image/gif", "image/jpg",
        "application/pdf", "application/x-pdf", "text/pdf"
    ]
    file_extension = image_file.filename.lower().split('.')[-1] if image_file.filename else ""
    allowed_extensions = ["png", "jpg", "jpeg", "gif", "pdf"]
    
    if image_file.content_type not in allowed_types and file_extension not in allowed_extensions:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail=f"Formato no soportado. Tipo detectado: {image_file.content_type}, extensión: {file_extension}. Use PNG, JPEG, GIF o PDF."
        )

    # --- 2) bloques de contenido ---
    content_blocks = [
        {
            "type": "text",
            "text": (
                "Eres un validador de certificados oficiales para justificación de faltas universitarias. "
                "Tu tarea es analizar la imagen y determinar SI ES un certificado válido.\n\n"
                "CERTIFICADOS VÁLIDOS:\n"
                "1. Certificado Cita Médica Sin Reposo\n"
                "2. Certificado Cita Médica Con Reposo\n"
                "3. Certificado Cita Médica Hijos Menores\n"
                "4. Certificado de Participación Deportiva\n"
                "5. Acta de Defunción (REQUIERE ELEVACIÓN AUTOMÁTICA A MENTOR)\n\n"
                "REGLA CRÍTICA PARA DIFERENCIAR CERTIFICADOS MÉDICOS:\n"
                "- SIN REPOSO: Solo certifica que el paciente asistió a una cita médica. NO indica días de descanso ni reposo.\n"
                "- CON REPOSO: Indica EXPLÍCITAMENTE días de reposo/descanso con fechas de inicio y fin. "
                "Debe decir algo como 'X días de reposo', 'reposo desde... hasta...', 'descanso médico'.\n"
                "- Si el certificado tiene diagnóstico pero NO menciona días de reposo, es SIN REPOSO.\n\n"
                "INSTRUCCIONES CRÍTICAS:\n"
                "- Si la imagen NO es uno de estos certificados oficiales (ej: foto personal, carpeta, captura de pantalla, "
                "documento genérico, etc.), responde ÚNICAMENTE: 'NO_ES_CERTIFICADO'\n"
                "- Si ES un Acta de Defunción, confirma que tiene los datos básicos del fallecido\n"
                "- Si es otro certificado válido, analiza sus campos según el tipo:\n\n"
                "Certificado Cita Médica SIN Reposo (solo certifica asistencia a cita): "
                "1. Nombres y Apellidos Completos del Paciente, "
                "2. Número de CI (cédula del paciente), "
                "3. Fecha y hora de atención, "
                "4. Diagnóstico o motivo de consulta (opcional), "
                "5. Firma y Sello del Doctor\n\n"
                "Certificado Cita Médica CON Reposo (indica días de descanso): "
                "1. Nombres y Apellidos Completos, "
                "2. Número de CI (cédula de identidad del paciente), "
                "3. Diagnóstico, "
                "4. Fecha y hora de atención, "
                "5. DÍAS DE REPOSO (número de días - OBLIGATORIO para este tipo), "
                "6. FECHAS DE REPOSO inicio y fin (OBLIGATORIO para este tipo), "
                "7. Firma y Sello del Doctor\n\n"
                "Certificado Cita Médica Hijos Menores: 1. Nombres y Apellidos del menor, "
                "2. Número de CI del menor, 3. Diagnóstico, 4. Cuidados asistidos (requiere cuidados del familiar), "
                "5. Nombres y Apellidos del familiar, 6. Número de Cédula del familiar, "
                "7. Justificación en días (número de días de reposo), 8. Fechas de reposo (fechas inicio y fin), "
                "9. Fecha de atención, 10. Firma y Sello del Doctor\n\n"
                "Certificado Actividad Deportiva: 1. Datos del Ente deportivo, 2. RUC del ente deportivo, "
                "3. Nombres y apellidos del estudiante, 4. Número de CI (cédula) del estudiante, "
                "5. Detalle del evento deportivo, 6. Fecha y Hora del evento, 7. Firma y Sello del Ente Deportivo\n\n"
                "Acta de Defunción (Calamidad Doméstica): "
                "1. Nombre completo del fallecido, 2. Fecha y lugar de fallecimiento, 3. Firma y sello del registro civil. "
                "NOTA: Este tipo SIEMPRE requiere intervención de un mentor humano.\n\n"
                "Si detectas un certificado válido (excepto Acta de Defunción), indica qué campos tiene y cuáles faltan de manera amable."
            )
        }
    ]

    # --- 3) imágenes ---
    is_image = (
        image_file.content_type in ("image/png", "image/jpeg", "image/gif", "image/jpg") or
        file_extension in ["png", "jpg", "jpeg", "gif"]
    )
    is_pdf = (
        image_file.content_type in ("application/pdf", "application/x-pdf", "text/pdf") or
        file_extension == "pdf"
    )
    
    if is_image:
        b64 = base64.b64encode(content).decode("utf-8")
        content_type = "image/jpeg" if file_extension == "jpg" else image_file.content_type
        data_url = f"data:{content_type};base64,{b64}"
        content_blocks.append({
            "type": "image_url",
            "image_url": {"url": data_url}
        })

    # --- 4) PDF ---
    elif is_pdf:
        if PdfReader is None:
            raise HTTPException(
                status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
                detail="PDF processing library not available. Please install PyPDF2 or pypdf."
            )
        try:
            reader = PdfReader(io.BytesIO(content))
            pages = [page.extract_text() or "" for page in reader.pages]
            texto_pdf = "\n\n".join(pages).strip()
            
            if not texto_pdf.strip():
                if fitz is not None:
                    pdf_document = fitz.open(stream=content, filetype="pdf")
                    if pdf_document.page_count > 0:
                        page = pdf_document[0]
                        mat = fitz.Matrix(2.0, 2.0)
                        pix = page.get_pixmap(matrix=mat)
                        img_data = pix.tobytes("png")
                        b64 = base64.b64encode(img_data).decode("utf-8")
                        data_url = f"data:image/png;base64,{b64}"
                        content_blocks.append({
                            "type": "image_url",
                            "image_url": {"url": data_url}
                        })
                        pdf_document.close()
                    else:
                        pdf_document.close()
                        raise HTTPException(
                            status_code=status.HTTP_400_BAD_REQUEST,
                            detail="El PDF está vacío o no contiene páginas."
                        )
                else:
                    raise HTTPException(
                        status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
                        detail="PyMuPDF no está disponible. No se pueden procesar PDFs escaneados."
                    )
            else:
                if len(texto_pdf) > 15000:
                    texto_pdf = texto_pdf[:15000] + "\n\n(...texto truncado...)"
                content_blocks.append({
                    "type": "text",
                    "text": "Texto extraído de PDF:\n\n" + texto_pdf
                })
        except HTTPException:
            raise
        except Exception as e:
            logger.error(f"Error procesando PDF: {e}", exc_info=True)
            raise HTTPException(
                status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
                detail=f"Error inesperado procesando el PDF: {str(e)}"
            )
    else:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail=f"Tipo de archivo no reconocido: {image_file.content_type}, extensión: {file_extension}"
        )

    # --- 5) llamada al modelo ---
    try:
        resp = await azure_client.chat.completions.create(
            model=settings.azure_openai_deployment_chat,
            messages=[
                {
                    "role": "system",
                    "content": (
                        "Eres un validador estricto de certificados oficiales. "
                        "Si la imagen NO es un certificado válido, responde ÚNICAMENTE 'NO_ES_CERTIFICADO'. "
                        "Si ES un certificado válido, analiza sus campos detalladamente."
                    )
                },
                {
                    "role": "system",
                    "content": (
                        "Si recibes un bloque que empiece con “Texto extraído de PDF:”, "
                        "trátalo exactamente igual que si fuera la imagen: "
                        "analiza la información para detectar el tipo de certificado y sus campos."
                    )
                },
                {
                    "role": "user",
                    "content": content_blocks
                }
            ],
            max_tokens=1000,
            temperature=0.1
        )

        analysis = resp.choices[0].message.content

        # ======================================================================
        # DETECCIÓN TEMPRANA: Si el modelo dice que NO es un certificado válido
        # ======================================================================
        if "NO_ES_CERTIFICADO" in (analysis or "").upper():
            summary = (
                "El archivo que subiste no corresponde a ningún certificado válido para justificación de faltas, por favor sube un certificado oficial"
            )
            escalated = ""
            fullName = ""
            date_init = ""
            date_end = ""
            identification = ""

            # Persistencia de estado OCR
            tag = "doc:no_certificado"
            add_uploaded_doc(session_id, tag)
            set_ocr_result(session_id, {
                "certificate": "Desconocido",
                "summary": summary,
                "escalated": escalated,
                "ts": datetime.utcnow().isoformat()
            })
            get_uploaded_docs(session_id).discard("ocr_notified")

            return success_response(data={
                "analysis": analysis,
                "summary": summary,
                "certificate": "Desconocido",
                "escalated": escalated,
                "fullName": fullName,
                "dateInit": date_init,
                "dateEnd":  date_end,
                "identification": identification 
            })

        # --- 6a) tipo de certificado ---
        allowed_labels = {
            "CitaMedicaSinReposo",
            "CitaMedicaConReposo",
            "CitaMedicaHijosMenores",
            "RepresentacionUniversitaria",
            "CalamidadDomestica",
            "Desconocido"
        }
        try:
            classify = await azure_client.chat.completions.create(
                model=settings.azure_openai_deployment_chat,
                messages=[
                    {
                        "role": "system",
                        "content": (
                            "Clasifica el tipo de certificado a partir del ANÁLISIS dado. "
                            "Responde con **una sola palabra** de esta lista EXACTA, sin espacios ni tildes: "
                            "CitaMedicaSinReposo | CitaMedicaConReposo | CitaMedicaHijosMenores | "
                            "RepresentacionUniversitaria | CalamidadDomestica | Desconocido. "
                            "Mapea: 'Certificado de Participación/Actividad Deportiva' -> RepresentacionUniversitaria; "
                            "'Acta de Defunción' -> CalamidadDomestica."
                        )
                    },
                    {
                        "role": "user",
                        "content": f"ANÁLISIS:\n{analysis}\n\nDevuelve solo una etiqueta de la lista."
                    }
                ],
                max_tokens=10,
                temperature=0
            )
            raw_label = (classify.choices[0].message.content or "").strip()
            normalized = "".join(ch for ch in raw_label if ch.isalnum())
            certificate = normalized if normalized in allowed_labels else "Desconocido"
        except Exception as e:
            logger.warning(f"No se pudo clasificar el tipo de certificado: {e}")
            certificate = "Desconocido"

        # ======================================================================
        # LÓGICA ESPECIAL: Acta de Defunción requiere ELEVACIÓN AUTOMÁTICA
        # ======================================================================
        if certificate == "CalamidadDomestica":
            # Para actas de defunción, siempre elevar al mentor
            summary = "--mentor--"
            escalated = "mentor"  # Marca especial para elevación automática
            fullName = ""
            date_init = ""
            date_end = ""
            identification = ""

            # Persistencia de estado OCR
            tag = "doc:CalamidadDomestica"
            add_uploaded_doc(session_id, tag)
            set_ocr_result(session_id, {
                "certificate": certificate,
                "summary": summary,
                "escalated": escalated,
                "ts": datetime.utcnow().isoformat()
            })
            get_uploaded_docs(session_id).discard("ocr_notified")

            return success_response(data={
                "analysis": analysis,
                "summary": summary,
                "certificate": certificate,
                "escalated": escalated,
                "fullName": fullName,
                "dateInit": date_init,
                "dateEnd":  date_end,
                "identification": identification 
            })

        # ======================================================================
        # NUEVA LÓGICA: Si no se reconoce un certificado, devolver mensaje amable
        # y omitir verificación y extracción de campos.
        # ======================================================================
        if certificate == "Desconocido":
            summary = (
                "El archivo que subiste no pertenece a ningún certificado que reconocemos."
            )
            escalated = ""
            fullName = ""
            date_init = ""
            date_end = ""
            identification = ""

            # Persistencia de estado OCR
            tag = "doc:desconocido"
            add_uploaded_doc(session_id, tag)
            set_ocr_result(session_id, {
                "certificate": certificate,
                "summary": summary,
                "escalated": escalated,
                "ts": datetime.utcnow().isoformat()
            })
            get_uploaded_docs(session_id).discard("ocr_notified")

            return success_response(data={
                "analysis": analysis,
                "summary": summary,
                "certificate": certificate,
                "escalated": escalated,
                "fullName": fullName,
                "dateInit": date_init,
                "dateEnd":  date_end,
                "identification": identification 
            })

        # --- 6b) resumen (para certificados reconocidos) ---
        summary_resp = await azure_client.chat.completions.create(
            model=settings.azure_openai_deployment_chat,
            messages=[
                {
                    "role": "system",
                    "content": "Eres un asistente conciso. Recibes un análisis detallado y debes responder en una frase si el certificado está completo puedes decir: el certificado tiene lo requerido, voy a realizar la solicitud para que te justifiquen la falta. Caso contrario si detectas que no cumple el certificado, solo responder brevemente los campos faltantes."
                },
                {
                    "role": "user",
                    "content": f"Análisis:\n{analysis}\n\nPor favor, dame un mensaje rapido y amable del estado del certificado analizado, “En el certificado nos hace falta más información para que nos puedan aceptar sin problema para la justificación, en este caso faltan: campo1, campo2...” si hay elementos pendientes."
                }
            ],
            max_tokens=100,
            temperature=0.0
        )
        summary = (summary_resp.choices[0].message.content or "").strip()

        # --- 6c) validación estricta ---
        escalated = ""
        try:
            check_resp = await azure_client.chat.completions.create(
                model=settings.azure_openai_deployment_chat,
                messages=[
                    {
                        "role": "system",
                        "content": (
                            "Eres un verificador estricto. Debes revisar si el documento cumple TODOS "
                            "los requisitos mínimos según su tipo. Responde EXACTAMENTE UNO de estos dos formatos:\n"
                            "OK\n"
                            "MISSING: campo1, campo2, campo3"
                        )
                    },
                    {
                        "role": "user",
                        "content": f"""
Tipo detectado: {certificate}

Requisitos mínimos por tipo (todos obligatorios):
- CitaMedicaSinReposo:
  Nombres y Apellidos; Cédula; Historia Clínica; Servicio prestado; Horario de atención (fecha y hora); Firma y Sello.
- CitaMedicaConReposo:
  Nombres y Apellidos; Cédula; Síntomas; Diagnóstico; Horario de Atención (fecha y hora);
  Días de reposo; Fechas de reposo (inicio y fin); Firma y Sello.
- CitaMedicaHijosMenores:
  Nombres y Apellidos del menor; Cédula del menor; Diagnóstico; Cuidados asistidos;
  Nombres y Apellidos del familiar; Cédula del familiar; Días de reposo; Fechas de reposo;
  Fecha de atención; Firma y Sello.
- RepresentacionUniversitaria (Actividad/Participación Deportiva):
  Datos del ente deportivo; RUC del ente; Nombres y Apellidos del estudiante; Cédula del estudiante;
  Detalle del evento; Fecha y hora del evento; Firma y Sello del ente.
- CalamidadDomestica (Acta de Defunción):
  Nombre completo del fallecido; Fecha y lugar de fallecimiento; Firma y sello del registro civil.

ANÁLISIS (texto del modelo con lo encontrado):
{analysis}

Indica solo 'OK' si TODAS las piezas requeridas están presentes. Si falta alguna o hay duda, responde 'MISSING: ...'.
""".strip()
                    }
                ],
                max_tokens=20,
                temperature=0
            )
            verdict = (check_resp.choices[0].message.content or "").strip()
            if verdict.upper().startswith("OK"):
                escalated = "justificado"
            else:
                escalated = ""
        except Exception:
            logger.exception("No se pudo verificar requisitos mínimos; dejando 'escalated' vacío.")
            escalated = ""

        # --- 6d) nombre completo (fullName) ---
        fullName = ""
        try:
            name_resp = await azure_client.chat.completions.create(
                model=settings.azure_openai_deployment_chat,
                messages=[
                    {
                        "role": "system",
                        "content": (
                            "Extrae el NOMBRE y APELLIDOS COMPLETOS del ESTUDIANTE/PACIENTE a partir del contenido. "
                            "Devuelve ÚNICAMENTE el nombre completo en MAYÚSCULAS, sin comillas ni etiquetas. "
                            "Si no se identifica con claridad, devuelve una cadena vacía."
                        ),
                    },
                    {
                        "role": "user",
                        "content": f"Contenido para búsqueda de nombre:\n{analysis}\n\nRespuesta (solo nombre completo):"
                    },
                ],
                max_tokens=30,
                temperature=0,
            )
            fullName = (name_resp.choices[0].message.content or "").strip().replace('"', "").replace("'", "")
            fullName = " ".join(fullName.replace("\n", " ").split())
            if fullName.upper() in {"", "N/A", "NO APLICA", "VACIO", "VACÍO", "DESCONOCIDO"}:
                fullName = ""
            else:
                fullName = fullName.upper()
        except Exception as e:
            logger.warning(f"No se pudo extraer el nombre del análisis: {e}")
            fullName = ""

        # --- 6e) fechas de inicio/fin (dateInit, dateEnd) ---
        date_init, date_end = "", ""
        try:
            dates_resp = await azure_client.chat.completions.create(
                model=settings.azure_openai_deployment_chat,
                messages=[
                    {
                        "role": "system",
                        "content": (
                            "A partir del contenido, identifica las FECHAS de INICIO y FIN del periodo "
                            "(por ejemplo, fechas de reposo, evento o atención). "
                            "Devuelve EXCLUSIVAMENTE un JSON con las claves EXACTAS: "
                            'dateInit (YYYY-MM-DD) y dateEnd (YYYY-MM-DD). '
                            "Si solo hay una fecha, úsala como dateInit y deja dateEnd como cadena vacía ''. "
                            "No agregues texto fuera del JSON."
                        ),
                    },
                    { "role": "user", "content": f"Contenido:\n{analysis}\n\nJSON:" },
                ],
                max_tokens=60,
                temperature=0,
            )
            raw = (dates_resp.choices[0].message.content or "").strip()
            parsed: dict = {}
            try:
                parsed = json.loads(raw)
            except Exception:
                parsed = {}

            if isinstance(parsed, dict):
                date_init = _normalize_any_date(parsed.get("dateInit", ""))
                date_end  = _normalize_any_date(parsed.get("dateEnd", ""))

            # Fallback si el JSON vino mal o no hay fechas
            if not date_init:
                ordered = _find_dates_in_text(analysis)
                if ordered:
                    date_init = ordered[0]
                    if len(ordered) > 1:
                        date_end = ordered[-1]
        except Exception as e:
            logger.warning(f"No se pudieron extraer fechas: {e}")
            date_init, date_end = "", ""

        # --- 6f) IDENTIFICACIÓN del estudiante (cédula/pasaporte) ---
        identification = ""
        try:
            id_resp = await azure_client.chat.completions.create(
                model=settings.azure_openai_deployment_chat,
                messages=[
                    {
                        "role": "system",
                        "content": (
                            "Del contenido proporcionado, devuelve EXCLUSIVAMENTE la identificación del ESTUDIANTE "
                            "(cédula/CI o pasaporte). Prioriza la del estudiante/paciente; ignora números de doctores, "
                            "RUC, folios o historia clínica. Responde SOLO el identificador en una sola cadena sin espacios, "
                            "sin comillas ni etiquetas. Si no está, devuelve vacío."
                        ),
                    },
                    { "role": "user", "content": analysis },
                ],
                max_tokens=15,
                temperature=0,
            )
            identification = (id_resp.choices[0].message.content or "").strip()
            identification = identification.replace(" ", "").replace("\n", "").upper()
            # sanity-check básico
            if not re.fullmatch(r"[A-Z0-9\-]{6,20}", identification or ""):
                identification = ""
        except Exception as e:
            logger.warning(f"No se pudo extraer identificación con IA: {e}")
            identification = ""

        # Fallback regex si IA no lo dio
        if not identification:
            clean = _remove_accents(analysis.lower())
            pattern = re.compile(
                r"(?:cedula|c[eé]dula|ci\b|numero\s+de\s+ci|pasaporte|passport)(?!.*\bruc\b)"
                r".{0,20}?([a-z0-9\-]{6,20})",
                re.IGNORECASE
            )
            m = pattern.search(analysis) or pattern.search(clean)
            if m:
                cand = m.group(1).replace(" ", "").upper()
                if re.fullmatch(r"[A-Z0-9\-]{6,20}", cand):
                    identification = cand

        # --- Persistencia de estado OCR ---
        tag = f"doc:{certificate}"
        add_uploaded_doc(session_id, tag)
        if certificate in {"CitaMedicaSinReposo", "CitaMedicaConReposo", "CitaMedicaHijosMenores"}:
            add_uploaded_doc(session_id, "certificado_medico")
        if escalated == "justificado":
            add_uploaded_doc(session_id, "certificado_validado")

        set_ocr_result(session_id, {
            "certificate": certificate,
            "summary": summary,
            "escalated": escalated,  # 'justificado' o ''
            "ts": datetime.utcnow().isoformat()
        })

        # permitir nueva notificación en el agent si se sube otro archivo en misma sesión
        get_uploaded_docs(session_id).discard("ocr_notified")

        return success_response(data={
            "analysis": analysis,
            "summary": summary,
            "certificate": certificate,
            "escalated": escalated,
            "fullName": fullName,
            "dateInit": date_init,
            "dateEnd":  date_end,
            "identification": identification 
        })

    except Exception as e:
        logger.error(f"Error procesando el archivo o generando summary: {e}", exc_info=True)
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail=f"Error procesando el archivo: {e}"
        )
