from datetime import datetime
from fastapi import APIRouter, File, UploadFile, HTTPException
from app.services.azure_openai_client import azure_openai_client
from fastapi.responses import JSONResponse
import tempfile
from typing import Optional
import os

from app.utils.response import success_response

router = APIRouter()

@router.post("/audio-to-text/", summary="Transcribe audio a text")
async def transcribe_audio(
    audio_file: UploadFile = File(...)):
    """
    Endpoint para transcribir archivos de audio usando Azure OpenAI.
    Máximo 25 MB por archivo.
    """
    
    # Validar tamaño del archivo (25 MB máximo)
    max_size = 25 * 1024 * 1024  # 25 MB en bytes
    
    # Leer contenido del archivo
    content = await audio_file.read()
    
    if len(content) > max_size:
        raise HTTPException(
            status_code=413,
            detail="El archivo es demasiado grande. Máximo 25 MB permitido."
        )
    
    # Validar tipo de archivo
    allowed_formats = [
        "audio/mpeg", "audio/mp3", "audio/wav", "audio/m4a", 
        "audio/mp4", "audio/webm", "audio/flac"
    ]
    
    if audio_file.content_type not in allowed_formats:
        raise HTTPException(
            status_code=400,
            detail="Formato de audio no soportado. Use MP3, WAV, M4A, MP4, WEBM o FLAC."
        )
    
    try:
        # Obtener cliente Azure OpenAI
        azure_client = azure_openai_client()

        # Crear archivo temporal
        with tempfile.NamedTemporaryFile(delete=False, suffix=f".{audio_file.filename.split('.')[-1]}") as temp_file:
            temp_file.write(content)
            temp_file_path = temp_file.name
        
        # Transcribir audio usando Azure OpenAI
        with open(temp_file_path, "rb") as audio_file_obj:
            transcript = await azure_client.audio.transcriptions.create(
                model="gpt-4o-mini-transcribe",
                file=audio_file_obj,
                response_format="text", 
            )
        
        # Limpiar archivo temporal
        os.unlink(temp_file_path)
        
        return success_response (
            data={
                "transcription": transcript,
                "timestamp": datetime.now().isoformat()
            },
            message="Transcripción realizada con éxito"
        )
    
    except Exception as e:
        # Limpiar archivo temporal en caso de error
        if 'temp_file_path' in locals():
            try:
                os.unlink(temp_file_path)
            except:
                pass
        
        raise HTTPException(
            status_code=500,
            detail=f"Error al procesar el archivo de audio: {str(e)}"
        )