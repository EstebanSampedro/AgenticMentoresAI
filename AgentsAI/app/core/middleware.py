from fastapi import FastAPI
from starlette.middleware.base import BaseHTTPMiddleware

class SecurityHeadersMiddleware(BaseHTTPMiddleware):
    def __init__(self, app, enable_hsts: bool = False):
        super().__init__(app)
        self.enable_hsts = enable_hsts
        # Rutas que necesitan CSP relajada o sin CSP (Swagger/Redoc/OpenAPI/assets)
        self._swagger_paths = ("/docs", "/redoc", "/openapi.json", "/static")

    async def dispatch(self, request, call_next):
        res = await call_next(request)

        # Headers seguros base
        res.headers["X-Content-Type-Options"] = "nosniff"
        res.headers["X-Frame-Options"] = "DENY"
        res.headers["Referrer-Policy"] = "no-referrer"

        # HSTS solo si sirves por HTTPS y en producción
        if self.enable_hsts and request.url.scheme == "https":
            res.headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains"

        # No apliques CSP estricta a Swagger/Redoc/OpenAPI
        if not request.url.path.startswith(self._swagger_paths):
            res.headers["Content-Security-Policy"] = (
                "default-src 'self'; "
                "img-src 'self' data:; "
                "style-src 'self' 'unsafe-inline'; "
                "script-src 'self' 'unsafe-inline';"
            )
        # Si quieres, podrías poner una CSP también para Swagger, pero con 'unsafe-inline'

        return res

def setup_middlewares(app: FastAPI, *, prod: bool = False) -> None:
    app.add_middleware(SecurityHeadersMiddleware, enable_hsts=prod)
