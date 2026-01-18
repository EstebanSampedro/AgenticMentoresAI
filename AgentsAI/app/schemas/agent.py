from pydantic import BaseModel, Field
from typing import Optional


class AgentRequest(BaseModel):
    """Request body para interacción con el agente - protege PII de exposición en URLs"""
    prompt: str = Field(..., description="El mensaje del usuario")
    session_id: str = Field(..., description="Un identificador único para esta conversación")
    fullName: str = Field(..., description="Nombre completo del usuario")
    nickname: str = Field(..., description="Apodo o alias")
    idCard: str = Field(..., description="Cédula de identidad")
    career: str = Field(..., description="Carrera académica")
    email: str = Field(..., description="Correo electrónico")
    student_gender: str = Field(..., description="Género del estudiante (M/F)")
    mentor_gender: str = Field(..., description="Género del mentor (M/F)")


class AgentResponse(BaseModel):
    """Response del agente conversacional"""
    session_id: str
    prompt: str
    response: str
    timestamp: str
    escalated: Optional[bool] = False
