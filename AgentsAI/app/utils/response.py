from typing import Any, Optional
from datetime import datetime, timezone
from fastapi import HTTPException
from fastapi.responses import JSONResponse
from app.schemas.response import APIResponse
import logging

logger = logging.getLogger(__name__)

def format_response(status: str, code: int, data: Optional[Any] = None, 
                    message: Optional[str] = None, errors: Optional[Any] = None, meta: Optional[Any] = None):
    """
    Formatea la respuesta de la API de manera estandarizada.
    """
    return {
        "status": status,
        "code": code,        
        "message": message,
        "data": data,
        "errors": errors,
        "meta": meta
    }

def success_response(
    *, 
    success: bool = True, 
    code: int = 200, 
    message: str = "Operation successful",
    data: Any = None,  
    errors: Any = None,  
    meta: Optional[Any] = None
) -> JSONResponse:
    """
    Genera una respuesta de éxito estandarizada.
    """
    try:
        response = APIResponse(
            success=success,
            code=code,
            message=message,
            data=data,
            errors=errors,
            meta=meta
        )
        return JSONResponse(status_code=code, content=response.model_dump())
    except Exception as e:
        logger.error(f"Error creating success response: {e}")
        # Fallback response
        return JSONResponse(
            status_code=500,
            content={"success": False, "code": 500, "message": "Internal server error"}
        )

def error_response(code: int, message: str, errors: Optional[Any] = None) -> None:
    """
    Genera una respuesta de error y lanza HTTPException.
    """
    try:
        response_data = APIResponse(
            success=False, 
            code=code, 
            message=message, 
            errors=errors
        ).model_dump()
    except Exception as e:
        logger.error(f"Error creating error response: {e}")
        response_data = {"success": False, "code": code, "message": message}
    
    raise HTTPException(status_code=code, detail=response_data)


def unauthorized_response(detail: str = "Not authenticated") -> JSONResponse:
    """Return standardized unauthorized response without raising HTTPException.

    This is useful inside global exception handlers where we already intercepted
    the original HTTPException (401/403) and want to emit the unified schema.
    """
    payload = APIResponse(
        success=False,
        code=401,
        message="Unauthorized",
        data={
            "detail": detail,
            "timestamp": datetime.now(timezone.utc).isoformat()
        },
        errors=None,
        meta=None
    ).model_dump()
    return JSONResponse(status_code=401, content=payload)
