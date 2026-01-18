import logging
from agents import Agent, function_tool
from app.services.openai_client import get_openai_client
from app.core.config import settings

# Configurar logger para este módulo
logger = logging.getLogger(__name__)
client = get_openai_client()

@function_tool
async def search_faq(query: str) -> str:
    """
    Busca en el vector store de políticas/FAQs y devuelve 
    textualmente los fragmentos más relevantes sin parafrasear.
    """
    logger.info(f"Searching FAQs for query: {query}")
    try:
        results = await client.vector_search(
            query=query,
            vector_store_id=settings.openai_vs_faq_id,
            max_num_results=3
        )
        if not results:
            return "No se encontraron respuestas relevantes en las FAQs."

        # Extrae y une los fragmentos recuperados
        snippets = [hit.content[0].text.strip() for hit in results]
        return "\n\n".join(snippets)

    except Exception as e:
        logger.error(f"Error during FAQ search: {e}")
        return "Ocurrió un error al buscar en las FAQs."

# Ajuste en las instrucciones para forzar uso de la herramienta
faq_agent = Agent(
    name="FAQAgent",
    instructions="""
Eres un asistente experto en la normativa de ausencias universitarias.  
**Para responder, usa ÚNICAMENTE la herramienta search_faq**  
que recupera tu base de políticas.  
Devuelve **exactamente** el texto recuperado (sin resumir ni parafrasear)  
y, si desconoces, responde que no tienes información al respecto y no inventes información.
""",
    tools=[search_faq],
)
