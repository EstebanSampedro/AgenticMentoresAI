from openai import AsyncAzureOpenAI
from agents import set_default_openai_client, set_default_openai_api, set_tracing_disabled
from app.core.config import settings
from typing import Optional
import logging

logger = logging.getLogger(__name__)

# Variable global para almacenar el cliente
azure_client: Optional[AsyncAzureOpenAI] = None


def azure_openai_client() -> AsyncAzureOpenAI:
    """
    Configura el cliente de Azure OpenAI como cliente por defecto
    para el Agents SDK y forza el uso de chat_completions.
    """
    global azure_client

    if azure_client is not None:
        logger.debug("Azure OpenAI client already configured, returning existing instance")
        return azure_client

    try:
        # Desactiva el tracing para evitar envíos a api.openai.com
        set_tracing_disabled(True)
        
        # Cliente específico para Agents SDK
        azure_client = AsyncAzureOpenAI(
            api_key=settings.azure_openai_api_key,
            api_version=settings.azure_openai_api_version,
            azure_endpoint=settings.azure_openai_endpoint,
            azure_deployment=settings.azure_openai_deployment,
        )


        # Registrar el cliente Azure en el Agents SDK
        set_default_openai_client(azure_client)
        set_default_openai_api("chat_completions")
        
        logger.info("Azure OpenAI client configured successfully")

        return azure_client
        
    except Exception as e:
        logger.error(f"Failed to configure Azure OpenAI client: {e}")
        raise RuntimeError(f"Could not configure Azure OpenAI client: {e}")
    
azure_client = azure_openai_client()
