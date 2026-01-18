from pydantic import BaseModel, Field
from typing import Optional, List, Dict, Any, Union


class SummaryRequest(BaseModel):
    """Request body para resumen de conversaciones - protege session_id en body"""
    session_id: Optional[str] = Field(None, description="Si se envía, se resume el historial guardado de esta sesión")
    conversation: Optional[str] = Field(None, description="Alternativa: pasa aquí el texto de la conversación completa")


class SummaryData(BaseModel):
    """Datos del resumen generado"""
    overview: str
    key_points: List[str]
    escalated: bool
    escalation_reason: str
    theme: str
    priority: str


class SummaryResponse(BaseModel):
    """Response del resumen de conversación"""
    summary: SummaryData
    timestamp: str
    session_id: Optional[str] = None
