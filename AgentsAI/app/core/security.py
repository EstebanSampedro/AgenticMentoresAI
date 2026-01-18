# app/core/security.py
from __future__ import annotations

import os
import re
import logging
from typing import Any, Dict, List, Optional

import jwt
from jwt import PyJWKClient, InvalidIssuerError, InvalidAudienceError
from fastapi import HTTPException, Security, Depends
from fastapi.security import HTTPBearer
from pydantic import BaseModel

logger = logging.getLogger(__name__)

# ============================================================
# Config (.env)
# ============================================================
TENANT_ID: str = (os.getenv("TENANT_ID", "") or "").strip()
CLIENT_ID: str = (os.getenv("CLIENT_ID", "") or "").strip()
RAW_ISSUER: str = (os.getenv("ISSUER", "") or "").strip()
ENV_AUDIENCE: str = (os.getenv("AUDIENCE", "") or "").strip()

security = HTTPBearer(auto_error=True)


# ============================================================
# Helpers de normalización
# ============================================================
def _ensure_https(url: str) -> str:
    if not url:
        return ""
    if url.startswith("http://"):
        url = "https://" + url[len("http://") :]
    if not url.startswith("https://"):
        url = "https://" + url
    return url


def _issuer_candidates(raw_iss: str, tenant: str) -> List[str]:
    """
    Devuelve una lista de *candidatos* de issuer aceptados.
    Acepta variantes con y sin barra final, sts.windows.net y login.microsoftonline v2.0.
    """
    cands: List[str] = []

    if raw_iss:
        base = _ensure_https(raw_iss).rstrip("/")
        # exacto, y con barra final
        cands += [base, base + "/"]
        # si es login.microsoftonline y no trae /v2.0, agrega también v2.0
        if "login.microsoftonline.com" in base and not base.endswith("/v2.0"):
            cands += [base + "/v2.0", base + "/v2.0/"]
    elif tenant:
        # por defecto usa v2.0
        base = f"https://login.microsoftonline.com/{tenant}/v2.0"
        cands += [base, base + "/"]

    if tenant:
        # compatibilidad con emisores v1 de AAD
        sts = f"https://sts.windows.net/{tenant}"
        cands += [sts, sts + "/"]

    # dedup preservando orden
    out: List[str] = []
    seen: set[str] = set()
    for i in cands:
        if i not in seen:
            seen.add(i)
            out.append(i)
    return out


def _audience_candidates(env_aud: str, client_id: str) -> List[str]:
    """
    Devuelve una lista de audiencias aceptadas (api://GUID y GUID puro).
    """
    cands: List[str] = []
    if env_aud:
        cands.append(env_aud.strip())
    if client_id:
        cands += [f"api://{client_id}", client_id]
    # dedup
    out: List[str] = []
    seen: set[str] = set()
    for a in cands:
        if a and a not in seen:
            seen.add(a)
            out.append(a)
    return out


ISSUER_CANDIDATES: List[str] = _issuer_candidates(RAW_ISSUER, TENANT_ID)
AUDIENCE_CANDIDATES: List[str] = _audience_candidates(ENV_AUDIENCE, CLIENT_ID)


# ============================================================
# Modelos
# ============================================================
class User(BaseModel):
    username: Optional[str] = None
    email: Optional[str] = None
    name: Optional[str] = None
    roles: List[str] = []
    scopes: List[str] = []


# ============================================================
# Validación JWT (RS256 + JWKs)
# ============================================================
def _jwks_url_from_issuer(issuer: str) -> str:
    return f"{issuer.rstrip('/')}/discovery/keys"


def _decode_no_verify(token: str) -> Dict[str, Any]:
    try:
        return jwt.decode(token, options={"verify_signature": False})
    except Exception:
        return {}


def _try_decode_with(issuer: str, audience: str, token: str) -> Dict[str, Any]:
    jwks_url = _jwks_url_from_issuer(issuer)
    jwks_client = PyJWKClient(jwks_url)
    signing_key = jwks_client.get_signing_key_from_jwt(token).key
    return jwt.decode(
        token,
        signing_key,
        algorithms=["RS256"],
        audience=audience,
        issuer=issuer,
    )


def get_token_payload(credentials=Security(security)) -> Dict[str, Any]:
    """
    Valida el JWT Bearer recibido contra una lista de (issuer, audience) candidatos.
    Acepta variantes con/sin '/' final y v2.0. Lanza 401 con detalle claro si falla.
    """
    token = credentials.credentials

    if not ISSUER_CANDIDATES or not AUDIENCE_CANDIDATES:
        raise HTTPException(
            status_code=500,
            detail="Configuración de seguridad incompleta (ISSUER/AUDIENCE).",
        )

    last_error: Optional[Exception] = None

    for iss in ISSUER_CANDIDATES:
        try:
            jwks_client = PyJWKClient(_jwks_url_from_issuer(iss))
            key = jwks_client.get_signing_key_from_jwt(token).key
        except Exception as e:
            last_error = e
            continue

        for aud in AUDIENCE_CANDIDATES:
            try:
                payload = jwt.decode(
                    token, key, algorithms=["RS256"], audience=aud, issuer=iss
                )
                return payload
            except (InvalidAudienceError, InvalidIssuerError, jwt.PyJWTError) as e:
                last_error = e
                continue

    decoded = _decode_no_verify(token)
    actual_iss = decoded.get("iss")
    actual_aud = decoded.get("aud")
    detail = (
        f"No se pudo validar el token. iss='{actual_iss}', aud='{actual_aud}'. "
        f"Esperado iss ∈ {ISSUER_CANDIDATES} y aud ∈ {AUDIENCE_CANDIDATES}."
    )
    raise HTTPException(status_code=401, detail=f"{detail} ({last_error})")


# ============================================================
# Autorización: roles / scopes
# ============================================================
def _extract_roles(payload: Dict[str, Any]) -> List[str]:
    roles = payload.get("roles") or []
    if isinstance(roles, str):
        roles = [roles]
    return [str(r) for r in roles]


def _extract_scopes(payload: Dict[str, Any]) -> List[str]:
    scp = payload.get("scp") or payload.get("scope") or ""
    if isinstance(scp, list):
        scopes = scp
    else:
        scopes = re.split(r"\s+", scp.strip()) if scp else []
    return [s for s in scopes if s]


def require_roles(required: List[str]):
    """
    Autoriza si el usuario tiene al menos uno de los roles requeridos.
    Si el token no trae 'roles', también se permite validar por 'scp' (scopes).
    """
    required = [r.strip() for r in (required or []) if r and r.strip()]

    def _checker(payload: Dict[str, Any] = Depends(get_token_payload)) -> Dict[str, Any]:
        user_roles = _extract_roles(payload)
        user_scopes = _extract_scopes(payload)

        if not required:
            return payload

        if any(r in user_roles for r in required):
            return payload

        if any(r in user_scopes for r in required):
            return payload

        raise HTTPException(status_code=403, detail="Permisos insuficientes")

    return _checker


# ============================================================
# Helper: mapear payload -> User
# ============================================================
def payload_to_user(payload: Dict[str, Any]) -> User:
    username = (
        payload.get("preferred_username") or payload.get("upn") or payload.get("sub")
    )
    email = payload.get("email") or payload.get("preferred_username")
    name = payload.get("name")
    return User(
        username=username,
        email=email,
        name=name,
        roles=_extract_roles(payload),
        scopes=_extract_scopes(payload),
    )
