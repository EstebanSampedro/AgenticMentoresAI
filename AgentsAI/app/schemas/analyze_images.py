from typing import Any, Optional
from pydantic import BaseModel, Field


class ImageAnalysisRequest(BaseModel):
    """Request body para análisis de imágenes - evita exposición de PII en URLs"""
    session_id: str = Field(..., description="ID de la sesión para agrupar documentos")


class ImageAnalysisResponse(BaseModel):
    """Response del análisis de documentos/certificados"""
    analysis: str
    summary: str
    certificate: str
    escalated: str
    fullName: str = Field(default="", alias="fullName")
    dateInit: str = Field(default="", alias="dateInit")
    dateEnd: str = Field(default="", alias="dateEnd")
    identification: str = Field(default="")
    
    class Config:
        populate_by_name = True