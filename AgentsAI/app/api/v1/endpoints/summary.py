# app/api/v1/endpoints/summary.py
from __future__ import annotations

import json
import logging
from datetime import datetime, timezone
from typing import Optional, List, Dict, Tuple

from fastapi import APIRouter, HTTPException, Body, Depends
from app.services.azure_openai_client import azure_openai_client
from app.utils.response import success_response
from app.utils.session_store import get_session_messages, get_ocr_result
from app.utils.escalamiento_detector import detectar_escalamiento  # detección determinística
from app.core.config import settings
from app.core.security import User
from app.core.security import get_token_payload
from app.schemas.summary import SummaryRequest, SummaryResponse

logger = logging.getLogger(__name__)
router = APIRouter(
    dependencies=[Depends(get_token_payload)]
)


def _render_history_for_summary(messages: List[Dict[str, str]]) -> str:
    """
    Convierte la lista de mensajes de la sesión en un texto plano
    tipo chat: 'Estudiante:' / 'Mentor:' para que el modelo resuma mejor.
    """
    lines: List[str] = []
    for m in messages:
        role = m.get("role", "")
        content = m.get("content", "")
        if role == "user":
            lines.append(f"Estudiante: {content}")
        elif role == "assistant":
            lines.append(f"Mentor: {content}")
        else:
            lines.append(f"{role}: {content}")
    return "\n".join(lines)


def _detect_local_escalation(messages: List[Dict[str, str]], fallback_text: Optional[str]) -> Tuple[bool, str]:
    """
    Reglas determinísticas de escalado:
    - Si algún mensaje del asistente contiene '--mentor--' -> escalado True.
    - Si el texto acumulado del usuario activa detectar_escalamiento(...) -> escalado True.
    - Si el fallback_text (conversation directa) activa detectar_escalamiento(...) -> escalado True.
    Devuelve (escalado_bool, motivo_txt). El motivo lo generará la IA si está vacío.
    """
    # ¿El asistente ya escaló explícitamente?
    if any((m.get("role") == "assistant") and ("--mentor--" in (m.get("content") or "")) for m in messages):
        return True, ""  # motivo lo genera la IA

    # Texto del usuario (todo lo que dijo en la sesión)
    user_text = " ".join(m.get("content", "") for m in messages if m.get("role") == "user")
    
    # Si no hay texto de usuario en messages, usar el fallback (conversation directa)
    if not user_text.strip() and fallback_text:
        user_text = fallback_text

    # ¿El texto activa el detector de escalamiento?
    if user_text and detectar_escalamiento(user_text):
        return True, ""  # motivo lo genera la IA

    # También verificar si el fallback contiene '--mentor--' (por si viene en conversation)
    if fallback_text and "--mentor--" in fallback_text.lower():
        return True, ""

    return False, ""


async def _infer_escalation_reason_llm(conversation_text: str) -> str:
    """
    Pide a la IA que elija el motivo más grave y lo exprese en UNA frase corta,
    natural y específica, en tono profesional y empático.
    Si no hay motivo claro, devuelve "".
    """
    azure_client = azure_openai_client()
    resp = await azure_client.chat.completions.create(
        model=settings.azure_openai_deployment_chat,
        messages=[
            {
                "role": "system",
                "content": (
                    "Eres un analista de riesgo para un chat universitario. Lee la conversación completa "
                    "y redacta UNA sola oración (15–30 palabras) en tono profesional y empático que explique "
                    "por qué debe intervenir un mentor. Prioriza el factor más grave. Evita diagnósticos, juicios "
                    "o recomendaciones clínicas; no incluyas datos personales, comillas, ni listas. "
                    "Usa formulaciones neutrales como 'debido a...' o 'por presentar...'. "
                    "Devuelve únicamente la oración, sin texto adicional."
                )
            },
            {
                "role": "user",
                "content": f"Conversación:\n{conversation_text}\n\nOración:"
            },
        ],
        temperature=0.2,
        max_tokens=80,
    )
    sentence = (resp.choices[0].message.content or "").strip().replace("\n", " ")
    if "." in sentence:
        sentence = sentence.split(".")[0].strip() + "."
    if len(sentence) > 220:
        sentence = sentence[:220].rsplit(" ", 1)[0].rstrip() + "…"
    return sentence


async def _summarize_with_aoai(conversation_text: str, ocr_info: Optional[Dict] = None) -> Dict:
    """
    Llama a Azure OpenAI para producir un resumen estructurado.
    Devuelve un dict con las claves:
    - overview (str)
    - key_points (list[str])
    - escalated (bool)
    - escalation_reason (str)
    """
    # GUARD: si no hay nada que resumir, regresa vacío
    if not conversation_text or not conversation_text.strip():
        return {"overview": "", "key_points": [], "escalated": False, "escalation_reason": ""}

    azure_client = azure_openai_client()

    extra = ""
    if ocr_info:
        extra = (
            "\n\n[Estado de documentos detectado por OCR en la sesión]\n"
            f"- Tipo: {ocr_info.get('certificate', '')}\n"
            f"- Resumen: {ocr_info.get('summary', '')}\n"
            f"- Estado: {ocr_info.get('escalated', '')}\n"
        )

    resp = await azure_client.chat.completions.create(
        model=settings.azure_openai_deployment_chat,
        messages=[
            {
                "role": "system",
                "content": (
                    "Eres un asistente que resume conversaciones de chat. "
                    "Devuelve EXCLUSIVAMENTE un JSON con las claves EXACTAS: "
                    "overview (string), key_points (array de 3 a 6 strings), "
                    "escalated (boolean), escalation_reason (string). "
                    'Si no hubo escalamiento, usa escalated=false y escalation_reason="". '
                    "No incluyas texto fuera del JSON."
                ),
            },
            {
                "role": "user",
                "content": (
                    f"Conversación completa:\n{conversation_text}{extra}\n\n"
                    "Genera el JSON pedido."
                ),
            },
        ],
        temperature=0.2,
        max_tokens=600,
    )

    model_text = (resp.choices[0].message.content or "").strip()

    # Intentar parsear JSON; si falla, devolver contenido como overview simple
    try:
        parsed = json.loads(model_text)
        return {
            "overview": parsed.get("overview", ""),
            "key_points": parsed.get("key_points", []),
            "escalated": bool(parsed.get("escalated", False)),
            "escalation_reason": parsed.get("escalation_reason", ""),
        }
    except Exception:
        logger.warning("El modelo no devolvió JSON válido; devolviendo overview simple.")
        return {
            "overview": model_text,
            "key_points": [],
            "escalated": False,
            "escalation_reason": "",
        }


# =========================
# Clasificador de temática
# =========================
async def _classify_theme(conversation_text: str) -> str:
    """
    Devuelve exactamente uno:
      - 'justificación de falta'
      - 'consultas generales'
    Fallback heurístico si la IA no devuelve un literal válido.
    """
    allowed = {"justificación de falta", "consultas generales"}

    try:
        azure_client = azure_openai_client()
        resp = await azure_client.chat.completions.create(
            model=settings.azure_openai_deployment_chat,
            messages=[
                {
                    "role": "system",
                    "content": (
                        "Eres un clasificador binario para un chat universitario. "
                        "Lee la conversación y devuelve EXCLUSIVAMENTE uno de estos literales, sin texto adicional: "
                        "justificación de falta  |  consultas generales.\n\n"
                        "- Usa justificación de falta si el foco es justificar una inasistencia, "
                        "presentar certificados (médico, deportivo, acta de defunción), hablar de reposos, "
                        "fechas de faltas, validación de documentos, etc.\n"
                        "- Usa consultas generales para dudas de procesos, información general, preguntas que no "
                        "implican justificar faltas.\n"
                        "Devuelve solo el literal."
                    ),
                },
                {"role": "user", "content": conversation_text},
            ],
            temperature=0,
            max_tokens=3,
        )
        raw = (resp.choices[0].message.content or "").strip()
        if raw in allowed:
            return raw
    except Exception as e:
        logger.warning(f"No se pudo clasificar theme con IA: {e}")

    # Fallback por palabras clave
    text = (conversation_text or "").lower()
    justi_kw = [
        "justific", "falta", "inasist", "certificado", "reposo",
        "cita médica", "cita medica", "acta de defunción", "defunción",
        "deportiv", "torneo", "competenc", "representación", "representacion",
        "calamidad", "duelo", "fallec", "muerte", "permiso", "documento"
    ]
    if any(k in text for k in justi_kw):
        return "justificación de falta"
    return "consultas generales"


# =========================
# NUEVO: inferencia de prioridad (baja | media | alta)
# =========================
def _infer_priority(
    messages: List[Dict[str, str]],
    conversation_text: str,
    summary_payload: Dict,
    ocr_info: Optional[Dict],
    theme: str
) -> str:
    """
    Reglas:
    - 'alta'  si hubo escalamiento (escalated=True o '--mentor--' en mensajes).
    - 'media' si NO escaló pero hubo 'proceso' de IA (p.ej., justificación de faltas,
              solicitud/uso de documentos, OCR, pedir que suba archivos, etc.).
    - 'baja'  en el resto (consulta simple).
    """
    # Alta: escalado
    if summary_payload.get("escalated") is True:
        return "alta"
    if any((m.get("role") == "assistant") and ("--mentor--" in (m.get("content") or "")) for m in messages):
        return "alta"

    text = (conversation_text or "").lower()

    # Señales de "proceso" (media)
    process_kw = [
        "certific", "reposo", "sube", "adjunto", "archivo", "enviar documento",
        "ocr", "acta de defunción", "defunción", "deportiv", "torneo", "competenc",
        "ruc", "ente deportivo", "fecha del evento", "validar documento",
        "cita médica", "cita medica", "diagnóstico", "síntoma", "historia clínica"
    ]

    used_ocr = bool(ocr_info)  # hubo estado OCR en la sesión
    theme_jf = (theme == "justificación de falta")
    kw_hit = any(k in text for k in process_kw)

    if (used_ocr or theme_jf or kw_hit):
        return "media"

    # Resto: baja
    return "baja"


@router.post("/summary/", response_model=SummaryResponse, summary="Resume las conversaciones de un chat")
async def summarize(
    request: SummaryRequest = Body(...),
):
    """
    Si se pasa session_id, el resumen se arma con el historial guardado.
    Si no, puedes pasar un texto de conversación en 'conversation'.
    Además, se detecta determinísticamente (servidor) si hubo escalamiento.
    
    **SEGURIDAD**: session_id se recibe en el body para evitar exposición en URLs y logs.
    """
    # Extraer datos del request body
    session_id = request.session_id
    conversation = request.conversation
    
    try:
        if not session_id and not conversation:
            raise HTTPException(
                status_code=400,
                detail="Debes enviar 'session_id' o 'conversation'.",
            )

        messages: List[Dict[str, str]] = []
        convo_text = ""
        ocr_info: Optional[Dict] = None

        if session_id:
            messages = get_session_messages(session_id)
            convo_text = _render_history_for_summary(messages)
            ocr_info = get_ocr_result(session_id)
        else:
            convo_text = conversation or ""

        # GUARD: si la conversación está vacía, devuelve todo vacío (theme y priority vacíos)
        if not convo_text or not convo_text.strip():
            empty = {
                "overview": "",
                "key_points": [],
                "escalated": False,
                "escalation_reason": "",
                "theme": "",
                "priority": ""   # ← NUEVO cuando no hay contenido
            }
            return success_response(
                data={"summary": empty, "timestamp": datetime.now(timezone.utc).isoformat(), "session_id": session_id},
                message="Sin contenido de resumen",
            )

        # 1) Resumen del modelo
        summary_payload = await _summarize_with_aoai(convo_text, ocr_info)

        # 2) Detección local (determinística) de escalado
        local_escalado, _ = _detect_local_escalation(messages, conversation)

        # 3) Fusión: si local detecta escalado, forzamos 'escalated' y generamos motivo con IA si falta
        if local_escalado:
            summary_payload["escalated"] = True
            motive = (summary_payload.get("escalation_reason") or "").strip()
            if not motive:
                motive_llm = await _infer_escalation_reason_llm(convo_text)
                summary_payload["escalation_reason"] = motive_llm or "sensitive reasons detected in the conversation"

        # 4) Clasificar temática (si hay overview no vacío)
        if (summary_payload.get("overview") or "").strip():
            theme = await _classify_theme(convo_text)
        else:
            theme = ""  # si overview está vacío, theme vacío
        summary_payload["theme"] = theme

        # 5) NUEVO: Inferir prioridad
        priority = _infer_priority(messages, convo_text, summary_payload, ocr_info, theme) if theme != "" else ""
        summary_payload["priority"] = priority

        return success_response(
            data={
                "summary": summary_payload,
                "timestamp": datetime.now(timezone.utc).isoformat(),
                "session_id": session_id,
            },
            message="Resumen generado con éxito",
        )
    except HTTPException:
        raise
    except Exception as error:
        logger.exception(f"Error al generar el resumen: {error}")
        raise HTTPException(
            status_code=500,
            detail="Error al generar el resumen",
        )
