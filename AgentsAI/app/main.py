import uvicorn
from datetime import datetime, timezone

from fastapi import FastAPI, Request, HTTPException
from fastapi.responses import JSONResponse
from fastapi.middleware.cors import CORSMiddleware

from app.core.config import settings
from app.core.logging_config import LoggingConfig
from app.services.azure_openai_client import azure_openai_client
from app.api.v1.endpoints.agent import router as agent_router
from app.api.v1.endpoints.analyze_images import router as analyze_images_router
from app.api.v1.endpoints.audio_to_text import router as audio_to_text_router
from app.api.v1.endpoints.summary import router as summary_router
from app.api.v1.endpoints.auth import router as auth_router
from app.core.middleware import setup_middlewares
from app.utils.response import unauthorized_response, success_response



# Setup logging
# logger = LoggingConfig.setup_logging()

# Configurar cliente Azure en startup
azure_openai_client()

# Define API metadata
tags_metadata = [
    {
        "name": "Health",
        "description": "API health check endpoints.",
    },
    {
        "name": "Agents",
        "description": "Endpoints for managing AI agents.",
    },
     {
        "name": "IA services",
        "description": "Endpoints for AI services"
    },
]

# Initialize FastAPI app
app = FastAPI(
    title=settings.app_name, 
    description=f"UDLA {datetime.now().year} 🚀", 
    version=settings.version, 
    openapi_tags=tags_metadata,
    #openapi_url="/api/v1/openapi.json",
    docs_url="/docs", 
    #redoc_url="/redoc"
    # redoc_url=None, 
    debug=settings.debug
)

# CORS middleware
app.add_middleware(
    CORSMiddleware,
    allow_origins=settings.cors_origins,  # List of allowed origins
    allow_credentials=True,
    allow_methods=["*"],  # Allow all HTTP methods
    allow_headers=["*"],  # Allow all headers
)

setup_middlewares(app, prod=not settings.debug)


@app.exception_handler(HTTPException)
async def http_exception_handler(request: Request, exc: HTTPException):
    """Global handler to standardize 401/403 responses while preserving others.

    If status is 401/403 we wrap into our unified APIResponse schema.
    For other codes we let FastAPI default style but still convert to unified schema.
    """
    if exc.status_code in (401, 403):
        detail = exc.detail if isinstance(exc.detail, str) else getattr(exc.detail, 'get', lambda k, d=None: d)('message', 'Not authenticated')
        return unauthorized_response(detail if isinstance(detail, str) else 'Not authenticated')

    # For other HTTP errors return consistent structure
    message = exc.detail if isinstance(exc.detail, str) else 'Error'
    payload = {
        "success": False,
        "code": exc.status_code,
        "message": message,
        "data": None,
        "errors": None,
        "meta": None
    }
    return JSONResponse(status_code=exc.status_code, content=payload)


# Include routers
app.include_router(auth_router, prefix="/api/v1", tags=["Auth"])
app.include_router(agent_router, prefix="/api/v1/agents", tags=["Agents"])
app.include_router(analyze_images_router, prefix="/api/v1/analizeimages", tags=["IA services"])
app.include_router(audio_to_text_router, prefix="/api/v1/audiototext", tags=["IA services"])
app.include_router(summary_router, prefix="/api/v1/summary", tags=["IA services"])  

# Root endpoint
@app.get("/", tags=["Health"])
async def root():
    return {"status": "OK"}

# Health check endpoint
@app.get("/health", tags=["Health"])
async def health_check():
    """
    Endpoint de comprobación de estado para verificar que la API está funcionando.
    
    Returns:
        dict: Estado actual de la API, timestamp y versión
    """
    return {
        "status": "healthy", 
        "timestamp": datetime.now(timezone.utc).isoformat() + "Z", 
        "version": settings.version,
        "environment": "development" if settings.debug else "production"
    }


# Ejecutar el servidor con uvicorn
#if __name__ == "__main__":
#    uvicorn.run('main:app', host="0.0.0.0", port=8000, reload=True)
