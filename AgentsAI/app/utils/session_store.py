# app/utils/session_store.py

from typing import Dict, List, Set, Optional, Any

# Historias de chat
_sessions: Dict[str, List[Dict[str, str]]] = {}
# Documentos subidos (acta / cédula / certificados) por sesión
_uploaded_docs: Dict[str, Set[str]] = {}
# Perfiles de usuario por sesión
_profiles: Dict[str, Dict[str, str]] = {}
# Resultados de OCR/analizar-imágenes por sesión
_ocr_results: Dict[str, Dict[str, Any]] = {}


# -------- Historial de chat --------

def get_history(session_id: str) -> List[Dict[str, str]]:
    return _sessions.setdefault(session_id, [])

def append_message(session_id: str, role: str, content: str) -> None:
    _sessions.setdefault(session_id, []).append({"role": role, "content": content})

# Wrapper de solo-lectura (útil para summary)
def get_session_messages(session_id: str) -> List[Dict[str, str]]:
    """Devuelve una copia del historial de la sesión (o lista vacía)."""
    return list(_sessions.get(session_id, []))


# -------- Documentos subidos --------

def get_uploaded_docs(session_id: str) -> Set[str]:
    return _uploaded_docs.setdefault(session_id, set())

def add_uploaded_doc(session_id: str, doc_type: str) -> None:
    _uploaded_docs.setdefault(session_id, set()).add(doc_type)


# -------- Perfil del estudiante --------

def get_profile(session_id: str) -> Optional[Dict[str, str]]:
    """
    Devuelve el perfil guardado (nombre, apodo, cédula, carrera, facultad,
    semestre_actual, correo) o None si aún no existe.
    """
    return _profiles.get(session_id)

def set_profile(session_id: str, profile: Dict[str, str]) -> None:
    """Guarda o reemplaza el perfil del estudiante para la sesión."""
    _profiles[session_id] = profile


# -------- Estado OCR / Analyze Images --------

def set_ocr_result(session_id: str, result: Dict[str, Any]) -> None:
    """
    Guarda el resultado de análisis de imágenes para la sesión.
    Ejemplo de 'result':
      {
        "certificate": "CitaMedicaConReposo",
        "summary": "Texto breve...",
        "escalated": "justificado" | "",
        "ts": "2025-08-14T12:34:56Z"
      }
    """
    _ocr_results[session_id] = result

def get_ocr_result(session_id: str) -> Optional[Dict[str, Any]]:
    """Devuelve el último resultado de OCR para la sesión o None."""
    return _ocr_results.get(session_id)


# -------- Limpieza / Reset de contexto --------

def clear_history(session_id: str) -> None:
    """Elimina por completo el historial de chat de la sesión."""
    _sessions.pop(session_id, None)

def clear_uploaded_docs(session_id: str) -> None:
    """Elimina los documentos/etiquetas subidos asociados a la sesión."""
    _uploaded_docs.pop(session_id, None)

def clear_profile(session_id: str) -> None:
    """Elimina el perfil almacenado para la sesión."""
    _profiles.pop(session_id, None)

def clear_ocr_result(session_id: str) -> None:
    """Elimina el estado de OCR/Analyze Images para la sesión."""
    _ocr_results.pop(session_id, None)

def clear_session(session_id: str) -> None:
    """Limpieza total: historial + docs + perfil + OCR para la sesión."""
    clear_history(session_id)
    clear_uploaded_docs(session_id)
    clear_profile(session_id)
    clear_ocr_result(session_id)

# (Opcional) Reset global del entorno de pruebas
def clear_all() -> None:
    """Borra todas las sesiones, documentos, perfiles y OCR en memoria."""
    _sessions.clear()
    _uploaded_docs.clear()
    _profiles.clear()
    _ocr_results.clear()
