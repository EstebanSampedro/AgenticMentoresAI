# app/agents/operator_agent.py
from agents import Agent, function_tool
import logging, os, time, json, requests, urllib3
from requests import Session
from requests.exceptions import (
    HTTPError, SSLError, ReadTimeout, ConnectTimeout, RequestException
)
from datetime import datetime

# (opcional) .env
try:
    from dotenv import load_dotenv  # type: ignore
    load_dotenv(override=True)
except Exception:
    pass

logger = logging.getLogger(__name__)

# =========================
#  Configuración (ENV)
# =========================
INSECURE = os.getenv("BANNER_INSECURE", "0") == "1"
if INSECURE:
    urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)
    logger.warning("[operator_agent] BANNER_INSECURE=1 -> TLS verify=False (solo pruebas)")

BANNER_TOKEN_URL = os.getenv("BANNER_TOKEN_URL", "https://pruebaintegraciones.udla.edu.ec/BannerWebApi/token")
BANNER_API_BASE  = os.getenv("BANNER_API_BASE",  "https://pruebaintegraciones.udla.edu.ec/BannerWebApi")
BANNER_USERNAME  = os.getenv("BANNER_USERNAME",  "")
BANNER_PASSWORD  = os.getenv("BANNER_PASSWORD",  "")

# Clave del body para el correo institucional (igual a Postman)
BANNER_EMAIL_KEY = (os.getenv("BANNER_EMAIL_KEY", "institutionalEmail") or "institutionalEmail").strip()

# Endpoint oficial
BANNER_JUST_PATH = os.getenv("BANNER_JUST_PATH", "/api/GetStudentJustification")

# Timeout en segundos
BANNER_TIMEOUT_S = int(os.getenv("BANNER_TIMEOUT_S", "60"))

# =========================
#  Infra: Session + Cache
# =========================
_token_cache = {"access_token": None, "expires_at": 0.0}
_session: Session | None = None

def _get_session() -> Session:
    """Session que respeta HTTPS_PROXY/NO_PROXY."""
    global _session
    if _session is None:
        s = Session()
        s.trust_env = True
        s.headers.update({"User-Agent": "MentoresAI/1.0"})
        _session = s
    return _session

# =========================
#  Helpers HTTP
# =========================
def _get_banner_token() -> str:
    """Obtiene/cachea el token. Prueba /token y /Token por compatibilidad."""
    now = time.time()
    if _token_cache["access_token"] and now < _token_cache["expires_at"]:
        return _token_cache["access_token"]

    if not (BANNER_USERNAME and BANNER_PASSWORD):
        raise RuntimeError("Faltan BANNER_USERNAME/BANNER_PASSWORD")

    s = _get_session()
    data = {"grant_type": "password", "username": BANNER_USERNAME, "password": BANNER_PASSWORD}
    headers = {"Content-Type": "application/x-www-form-urlencoded"}

    urls = [BANNER_TOKEN_URL]
    if "/token" in BANNER_TOKEN_URL and "/Token" not in BANNER_TOKEN_URL:
        urls.append(BANNER_TOKEN_URL.replace("/token", "/Token"))

    last_exc: Exception | None = None
    for url in urls:
        try:
            resp = s.post(url, data=data, headers=headers, timeout=BANNER_TIMEOUT_S, verify=not INSECURE)
            resp.raise_for_status()
            payload = resp.json()
            access = payload.get("access_token") or payload.get("accessToken") or payload.get("token")
            expires_in = int(payload.get("expires_in") or payload.get("expiresIn") or 120)
            if not access:
                raise RuntimeError(f"Respuesta de token inválida: {payload}")

            _token_cache["access_token"] = access
            _token_cache["expires_at"]   = now + max(60, expires_in - 15)
            logger.info("[banner-token] OK (expira ~%ss)", expires_in)
            return access

        except (ReadTimeout, ConnectTimeout, SSLError, RequestException) as e:
            last_exc = e
            logger.warning("[banner-token] fallo %s en %s (timeout=%ss, insecure=%s)",
                           type(e).__name__, url, BANNER_TIMEOUT_S, INSECURE)

    raise last_exc or RuntimeError("No se pudo obtener token")

def _banner_post_json(path: str, body: dict) -> dict:
    """POST JSON con Bearer; refresca token si 401."""
    token = _get_banner_token()
    url = f"{BANNER_API_BASE.rstrip('/')}/{path.lstrip('/')}"
    s = _get_session()
    headers = {
        "Authorization": f"Bearer {token}",
        "Accept": "application/json",
        "Content-Type": "application/json",
    }

    def _do():
        return s.post(url, headers=headers, data=json.dumps(body),
                      timeout=BANNER_TIMEOUT_S, verify=not INSECURE)

    resp = _do()
    if resp.status_code == 401:
        _token_cache["access_token"] = None
        headers["Authorization"] = f"Bearer {_get_banner_token()}"
        resp = _do()

    try:
        resp.raise_for_status()
        return resp.json() if resp.text else {}
    except HTTPError:
        logger.error("[banner-post] %s %s => %s %s", url, body, resp.status_code, resp.text)
        raise

# =========================
#  Utilidades de datos
# =========================
def _pick(d: dict, *cands: str, default="s/d"):
    """Toma el primer valor no vacío (case-insensitive)."""
    for k in cands:
        if k in d and d[k] not in (None, ""):
            return d[k]
        kl = k.lower()
        if kl in d and d[kl] not in (None, ""):
            return d[kl]
    return default

_DATE_KEYS_PRIORITY = [
    # comunes
    "updateDate", "createdDate", "requestDate",
    # variantes UDLA
    "SZVMNTR_UPDATE_DATE", "szvmntr_update_date", "szvmntR_UPDATE_DATE",
    "SZVMNTR_CREATE_DATE", "szvmntr_create_date",
    "SZVMNTR_REQUEST_DATE", "szvmntr_request_date",
]

def _parse_dt(val: str | None) -> float:
    """
    Devuelve timestamp (epoch) para comparar; si no se puede, -inf.
    Acepta ISO y formatos habituales 'YYYY-MM-DD HH:MM[:SS]'.
    """
    if not val or not isinstance(val, str):
        return float("-inf")
    s = val.strip()
    # intentos comunes
    for fmt in ("%Y-%m-%dT%H:%M:%S.%fZ", "%Y-%m-%dT%H:%M:%S%z", "%Y-%m-%dT%H:%M:%S",
                "%Y-%m-%d %H:%M:%S", "%Y-%m-%d %H:%M", "%Y-%m-%d"):
        try:
            return datetime.strptime(s, fmt).timestamp()
        except Exception:
            continue
    # último recurso: reemplazos simples
    try:
        s2 = s.replace("T", " ").replace("Z", "")
        return datetime.fromisoformat(s2).timestamp()
    except Exception:
        return float("-inf")

def _pick_latest_item(items: list[dict]) -> dict:
    """Elige el item con fecha más reciente según _DATE_KEYS_PRIORITY; si todas fallan, el primero."""
    best = None
    best_ts = float("-inf")
    for it in items:
        ts = float("-inf")
        for k in _DATE_KEYS_PRIORITY:
            if k in it and it[k]:
                ts = max(ts, _parse_dt(str(it[k])))
        if ts == float("-inf"):
            # sin fecha utilizable: mantén orden original como desempate
            ts = -1.0 if best is None else best_ts - 1e-6
        if best is None or ts > best_ts:
            best = it
            best_ts = ts
    return best or (items[0] if items else {})

# =========================
#  Formateo de respuesta
# =========================
def _format_single_html(it: dict) -> str:
    """Render mínimo y humano para un solo caso (la última justificación)."""
    estado = _pick(it, "status", "SZVMNTR_ESTADO", "szvmntR_ESTADO", default="s/d")
    upd    = _pick(it, "updateDate", "SZVMNTR_UPDATE_DATE", "szvmntR_UPDATE_DATE", default="")

    # Línea pedida + gentil recordatorio de correo
    if upd and upd.lower() not in ("s/f", "s/d"):
        linea = (
            f"Claro te comento, el estado de tu justificación es: {estado} "
            f"(última actualización: {upd}). "
            f"Por favor, revisa y mantente atento a tu correo electrónico para más información."
        )
    else:
        linea = (
            f"Claro te comento, el estado de tu justificación es: {estado}. "
            f"Por favor, revisa y mantente atento a tu correo electrónico para más información."
        )
    return f"<p>{linea}</p>"

def _format_justification_html(api_response: dict, who: str) -> str:
    """
    Presenta /api/GetStudentJustification mostrando SOLO la última justificación.
    - 0 resultados: mensaje claro.
    - ≥1 resultado: una línea con el estado de la más reciente.
    """
    items = api_response.get("content") or []
    if not items:
        return f"<p>No encuentro solicitudes de justificación activas para {who}.</p>"

    latest = _pick_latest_item(items)
    return _format_single_html(latest)

# =========================
#  Tool expuesto
# =========================
@function_tool
def case_status_udla(institutional_email: str) -> str:
    """
    Consulta el estado de justificativos en UDLA Banner.
    Body enviado (por defecto): { "institutionalEmail": "<email>" }
    Devuelve SIEMPRE una sola línea con la última justificación.
    """
    email = (institutional_email or "").strip().lower()
    if not email:
        return "<p>Necesito tu correo institucional para consultar el estado de tu justificación.</p>"

    try:
        payload = {BANNER_EMAIL_KEY: email}
        resp = _banner_post_json(BANNER_JUST_PATH, payload)
        return _format_justification_html(resp, email)
    except Exception:
        logger.exception("[case_status_udla] Error consultando Banner")
        # Mensaje neutro, sin inventar datos
        return "<p>No pude consultar tu caso en este momento.</p>"

# =========================
#  Definición del agente
# =========================
operator_agent = Agent(
    name="OperatorAgent",
    instructions="""
Eres un operador técnico para UDLA Banner.
Cuando el estudiante pida el estado de su justificativo, llama a `case_status_udla(institutional_email)`.
Devuelve únicamente el HTML del tool (usa solo <p>).
Si no te dan el correo, tómalo de la línea 'DatosUsuario: ... correo=...'.
""",
    tools=[case_status_udla],
)
