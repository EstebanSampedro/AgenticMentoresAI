# app/api/v1/endpoints/auth.py
from fastapi import APIRouter, Depends
from pydantic import BaseModel

from app.core.security import (
    get_token_payload,
    require_roles,
    payload_to_user,
    User as SecurityUser,
)

router = APIRouter(
    prefix="/auth",
    tags=["authentication"],
    responses={401: {"description": "Unauthorized"}},
)

# ─────────────────────────────────────────────────────────────
# NOTA:
# Se migra a validación de Azure AD (Bearer JWT con JWKs).
# Ya NO se expone /token ni /refresh locales.
# El cliente debe enviar Authorization: Bearer <JWT de Azure>.
# ─────────────────────────────────────────────────────────────

class MeOut(BaseModel):
    username: str | None = None
    email: str | None = None
    name: str | None = None
    roles: list[str] = []
    scopes: list[str] = []

@router.get("/me", response_model=MeOut, summary="Datos del usuario autenticado (Azure AD)")
async def read_me(payload: dict = Depends(get_token_payload)):
    """Devuelve los datos básicos mapeados desde el JWT de Azure AD."""
    u: SecurityUser = payload_to_user(payload)
    return MeOut(**u.model_dump())

@router.get(
    "/me/secure",
    response_model=MeOut,
    summary="Prueba de autorización por rol/scope (require_roles)"
)
async def read_me_secure(
    payload: dict = Depends(require_roles(["Mentores.Read"]))  # <-- ajusta a tu rol/scope
):
    u: SecurityUser = payload_to_user(payload)
    return MeOut(**u.model_dump())
