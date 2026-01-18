# app/utils/tools.py
from datetime import datetime
from agents import function_tool
import logging
from app.services.openai_client import get_openai_client
from app.core.config import settings

logger = logging.getLogger(__name__)
client = get_openai_client()


@function_tool
def get_current_date() -> str:
    """
    Devuelve la fecha actual en formato DD-MM-YYYY.
    """
    return datetime.utcnow().strftime("%d-%m-%Y")

@function_tool
async def retrieve_justification_case(query: str) -> str:
    """
    Busca en el vector store de reglas de justificación
    y devuelve el JSON exacto con la clasificación.
    """
    logger.info(f"Searching JustificationRules for: {query}")
    try:
        hits = await client.vector_search(
            query=query,
            vector_store_id=settings.OPENAI_VS_INQUIRER_ID,
            max_num_results=1
        )
        if not hits:
            return '{"case":"desconocido","required_doc":null,"follow_up":[],"note":"","fecha_detectada":false}'
        # devolvemos tal cual el fragmento recuperado
        return hits[0].content[0].text.strip()
    except Exception as e:
        logger.error("Error retrieving justification rules", exc_info=True)
        # caemos en desconocido
        return '{"case":"desconocido","required_doc":null,"follow_up":[],"note":"","fecha_detectada":false}'

