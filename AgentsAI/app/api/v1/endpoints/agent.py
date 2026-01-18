# app/api/v1/endpoints/agent.py

from fastapi import APIRouter, Depends, HTTPException, Body
from typing import Any, Dict, Callable, Awaitable
from datetime import datetime, timezone
import re
import html

from app.utils.dep_agents    import get_manager
from app.utils.response      import success_response
from app.utils.session_store import (
    get_history,
    append_message,
    clear_session,
    get_uploaded_docs,
    get_ocr_result,
    add_uploaded_doc,
)
from app.utils.escalamiento_detector import detectar_escalamiento, obtener_mensaje_escalamiento
from app.core.security import User
from app.core.security import get_token_payload
from app.schemas.agent import AgentRequest, AgentResponse


router = APIRouter(
    dependencies=[Depends(get_token_payload)]
)

# ---------------- Detectar cierre de conversación ----------------
def _es_mensaje_cierre(texto: str) -> bool:
    """
    Detecta si el mensaje del usuario es ÚNICAMENTE un agradecimiento o cierre de conversación.
    Debe ser MUY preciso para evitar falsos positivos.
    
    SOLO retorna True si el mensaje es claramente un cierre/agradecimiento sin contenido adicional.
    """
    t = texto.lower().strip()
    
    # Si el mensaje contiene una pregunta, NO es cierre
    if "?" in t:
        return False
    
    # Si el mensaje es largo (más de 30 chars), probablemente tiene contenido real
    if len(t) > 30:
        return False
    
    # Si contiene palabras que indican que quiere más info, NO es cierre
    palabras_continuacion = [
        "no me", "no aparece", "no tengo", "no puedo", "no entiendo",
        "pero", "aunque", "sin embargo", "todavía", "aún", "aun",
        "cómo", "como", "qué", "que", "cuál", "cual", "dónde", "donde",
        "cuándo", "cuando", "por qué", "porque", "necesito", "quiero",
        "ayuda", "ayúdame", "explica", "dime", "otra", "más info",
        "nota", "falta", "error", "problema", "duda"
    ]
    
    for palabra in palabras_continuacion:
        if palabra in t:
            return False
    
    # Patrones de agradecimiento CLAROS (deben ser el contenido principal)
    patrones_cierre_exactos = [
        "gracias", "muchas gracias", "mil gracias", "te agradezco",
        "chao", "chau", "bye", "adiós", "adios", "hasta luego",
        "nos vemos", "cuídate", "cuidate", 
        "eso era todo", "era todo", "nada más", "nada mas",
        "no necesito más", "no necesito nada", "todo claro",
        "eso es todo", "no nada más", "no nada mas"
    ]
    
    # Verificar si el mensaje contiene un patrón de cierre claro
    for patron in patrones_cierre_exactos:
        if patron in t:
            return True
    
    # Mensajes MUY cortos que son claramente cierre (solo estas palabras exactas)
    cierres_cortos_exactos = [
        "ok", "okay", "vale", "listo", "entendido", "perfecto",
        "excelente", "genial", "de acuerdo", "está bien", "esta bien",
        "bueno", "dale", "claro"
    ]
    
    # Solo considerar cierre si el mensaje es CASI exclusivamente la palabra de cierre
    t_limpio = t.replace(",", "").replace(".", "").replace("!", "").strip()
    palabras = t_limpio.split()
    
    # Si tiene más de 3 palabras, probablemente NO es solo un cierre
    if len(palabras) > 3:
        return False
    
    # Verificar si es un cierre corto exacto
    for cierre in cierres_cortos_exactos:
        if t_limpio == cierre or t_limpio == f"{cierre} gracias":
            return True
    
    return False


def _generar_respuesta_cierre(nickname: str, mentor_gender: str) -> str:
    """
    Genera una respuesta amable de cierre cuando el estudiante agradece
    después de que su caso fue procesado.
    """
    # Variaciones para no ser repetitivo
    import random
    respuestas = [
        f"<p>De nada, {nickname}. Cualquier otra consulta, aquí estoy para ayudarte.</p>",
        f"<p>Con gusto, {nickname}. Si necesitas algo más, no dudes en escribirme.</p>",
        f"<p>Para servirte, {nickname}. Estoy aquí si tienes más preguntas.</p>",
        f"<p>Un placer ayudarte, {nickname}. Cuídate mucho.</p>",
    ]
    return random.choice(respuestas)
# ------------------------------------------------

# ---------------- HTML sanitizer ----------------
def _sanitize_html(text: str) -> str:
    """
    Sanitiza la salida del LLM permitiendo tags seguros: <p>, </p>, <br>, <a>.
    Previene ataques XSS pero permite HTML que el LLM genera intencionalmente.
    
    Si el LLM devuelve HTML válido (con <p>, <a>, etc.), lo respeta.
    Si devuelve texto plano, lo convierte a HTML simple.
    """
    if text is None:
        return "<p></p>"

    t = str(text).strip()

    # Si el modelo devuelve solo el handoff
    if t == "--mentor--":
        return "<p>--mentor--</p>"

    # Elimina fences de markdown si los hubiera
    t = re.sub(r"```(?:html)?", "", t).replace("```", "").strip()
    
    # Arreglar tags malformados como "<pTexto" → "<p>Texto"
    t = re.sub(r"<p([^>])", r"<p>\1", t)
    
    # Detectar si ya tiene tags HTML válidos
    has_html = bool(re.search(r"<[a-zA-Z][^>]*>", t))
    
    if has_html:
        # El LLM generó HTML intencionalmente, lo dejamos pasar
        # Solo sanitizamos scripts y eventos peligrosos
        t = re.sub(r'<script[^>]*>.*?</script>', '', t, flags=re.IGNORECASE | re.DOTALL)
        t = re.sub(r'\son\w+\s*=\s*["\'][^"\']*["\']', '', t, flags=re.IGNORECASE)
        
        # Si ya hay estructura <p>, retornar como está
        if "<p>" in t:
            return t
    
    # Si NO hay HTML, es texto plano: convertir a párrafos
    t = t.replace("\r\n", "\n")
    
    # Párrafos por doble salto de línea
    paras = [seg.strip() for seg in re.split(r"\n{2,}", t) if seg.strip()]
    if not paras:
        return "<p></p>"

    parts = []
    for seg in paras:
        # Convertir saltos simples en <br>
        seg_html = seg.replace("\n", "<br>")
        parts.append(f"<p>{seg_html}</p>")
    
    return "".join(parts)
# ------------------------------------------------


@router.post("/agent/", response_model=AgentResponse, summary="Interactúa con el Manager")
async def agent_endpoint(
    request: AgentRequest = Body(...),
    run: Callable[[str], Awaitable[str]] = Depends(get_manager),
) -> Dict[str, Any]:
    """
    Maneja el endpoint /agent/.
    - Si el usuario envía --reiniciar-- se limpia el contexto de esa sesión.
    - Revisa si el mensaje necesita ESCALAMIENTO inmediato (palabras críticas).
    - Si no hay escalamiento, intenta un FAST-PATH una sola vez: si existe un OCR recién subido,
      responde con ese estado y marca 'ocr_notified' para evitar bucles.
    
    **SEGURIDAD**: Datos sensibles (nombre, cédula, correo) se reciben en el body 
    para evitar exposición en URLs y logs del servidor.
    """
    # Extraer datos del request body
    prompt = request.prompt
    session_id = request.session_id
    fullName = request.fullName
    nickname = request.nickname
    idCard = request.idCard
    career = request.career
    email = request.email
    student_gender = request.student_gender
    mentor_gender = request.mentor_gender
    
    try:
        # ——— REINICIO EXPLÍCITO ———
        if prompt.strip().lower() == "--reiniciar--":
            clear_session(session_id)
            append_message(session_id, "system", "[contexto reiniciado]")
            return success_response(
                data={
                    "session_id": session_id,
                    "prompt": prompt,
                    "response": "<p>He reiniciado el contexto de la conversación. Empecemos de nuevo.</p>",
                    "timestamp": datetime.now(timezone.utc).isoformat(),
                },
                message="Contexto reiniciado",
            )

        # ——— PRE-ESCALAMIENTO ———
        if detectar_escalamiento(prompt):
            append_message(session_id, "user", prompt)
            append_message(session_id, "assistant", "--mentor--")
            return success_response(
                data={
                    "session_id": session_id,
                    "prompt": prompt,
                    "response": "<p>--mentor--</p>"
                },
                message=obtener_mensaje_escalamiento()
            )

        # ——— FAST-PATH: usar OCR solo UNA VEZ ———
        docs = get_uploaded_docs(session_id)   # set(...) de tags
        ocr  = get_ocr_result(session_id)      # {"certificate","summary","escalated","ts"} o None

        if (
            ocr
            and "ocr_notified" not in docs
            and (
                "certificado_validado" in docs
                or "certificado_medico" in docs
                or any(str(d).startswith("doc:") for d in docs)
            )
        ):
            append_message(session_id, "user", prompt)
            respuesta_ok = f"<p>{ocr['summary']}</p>"
            append_message(session_id, "assistant", respuesta_ok)
            add_uploaded_doc(session_id, "ocr_notified")

            return success_response(
                data={
                    "session_id": session_id,
                    "prompt": prompt,
                    "response": respuesta_ok,
                    "timestamp": datetime.now(timezone.utc).isoformat(),
                },
                message="Respuesta generada con estado de OCR de la sesión",
            )

        # ——— CIERRE DE CONVERSACIÓN: si ya se procesó el caso y el usuario agradece ———
        if (
            ocr
            and "ocr_notified" in docs
            and _es_mensaje_cierre(prompt)
        ):
            append_message(session_id, "user", prompt)
            respuesta_cierre = _generar_respuesta_cierre(nickname, mentor_gender)
            append_message(session_id, "assistant", respuesta_cierre)
            
            # Marcar que el caso está cerrado para evitar reactivaciones
            add_uploaded_doc(session_id, "caso_cerrado")

            return success_response(
                data={
                    "session_id": session_id,
                    "prompt": prompt,
                    "response": respuesta_cierre,
                    "timestamp": datetime.now(timezone.utc).isoformat(),
                },
                message="Respuesta de cierre de conversación",
            )

        # 1) Recupera la historia previa
        history = get_history(session_id)

        # 2) Añade el nuevo mensaje de usuario a la historia
        append_message(session_id, "user", prompt)

        # 3) Cuenta cuántas veces ya respondió Antonella
        assistant_count = sum(1 for m in history if m["role"] == "assistant")
        interaction = assistant_count + 1

        # 4) Reconstruye el “chat style” con prefijos
        full_prompt = f"Interaccion: {interaction}\n"
        for msg in history:
            prefix = "Estudiante:" if msg["role"] == "user" else "Mentor:"
            full_prompt += f"{prefix} {msg['content']}\n"

        # 5) Inyecta metadatos del usuario
        full_prompt += (
            f"DatosUsuario: nombre={fullName}, apodo={nickname}, "
            f"cédula={idCard}, carrera={career}, correo={email}, estudiante_genero={student_gender}, mentor_genero={mentor_gender}\n"
            "Mentor:"
        )

        # 6) Lanza el agente con TODO el contexto
        assistant_response = await run(full_prompt)

        # 6.1) Sanea/normaliza el HTML antes de guardar y devolver
        assistant_response = _sanitize_html(assistant_response)

        # 7) Guarda la respuesta del agente en la historia
        append_message(session_id, "assistant", assistant_response)

        # 8) Devuelve la respuesta al cliente
        return success_response(
            data={
                "session_id": session_id,
                "prompt": prompt,
                "fullName": fullName,
                "nickname": nickname,
                "idCard": idCard,
                "career": career,
                "email": email,
                "student_gender": student_gender,
                "mentor_gender": mentor_gender,
                "response": assistant_response,
                "timestamp": datetime.now(timezone.utc).isoformat(),
            },
            message="Respuesta generada por el agente Manager",
        )

    except Exception as error:
        raise HTTPException(status_code=500, detail=str(error))
