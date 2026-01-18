from typing import List
from pydantic import AnyHttpUrl, Field, ConfigDict, SecretStr, field_validator
from pydantic_settings import BaseSettings


class Settings(BaseSettings):
    """Application settings configuration.
    
    Loads configuration from environment variables or .env file.
    """
    # Application settings
    app_name: str = "UDLA Agents AI"
    version: str = "0.0.1"
    debug: bool = False
    
    # Security settings
    secret_key: str = Field(default="your_secret_key", env="SECRET_KEY")
    algorithm: str = "HS256"
    access_token_expires: int = 30  # en minutos
    refresh_token_expires: int = 12 * 60 

    auth_username: str = Field(default="testuser", env="AUTH_USERNAME")
    auth_password_hash: SecretStr = Field(default=SecretStr(""), env="AUTH_PASSWORD_HASH")
    
    # Azure OpenAI settings
    azure_openai_api_key: str = Field(..., env="AZURE_OPENAI_API_KEY")
    azure_openai_api_version: str = Field(..., env="AZURE_OPENAI_API_VERSION")
    azure_openai_endpoint: str = Field(..., env="AZURE_OPENAI_ENDPOINT")
    azure_openai_deployment: str = Field(..., env="AZURE_OPENAI_DEPLOYMENT")
    azure_openai_deployment_chat: str = Field(..., env="AZURE_OPENAI_DEPLOYMENT_CHAT")

    # OpenAI settings
    openai_api_key: str = Field(..., env="OPENAI_API_KEY")
    openai_vs_faq_id: str = Field(..., env="OPENAI_VS_FAQ_ID")
    openai_vs_inquirer_id: str = Field(..., env="OPENAI_VS_INQUIRER_ID")
 
    # CORS settings
    cors_origins: List[AnyHttpUrl] = []

    # Prefix for API routes
    api_prefix: str = "/api/v1"
    
    # Optional: Add environment-specific config
    environment: str = Field(default="development", env="ENVIRONMENT")
    
    model_config = ConfigDict(
        env_file=".env",
        env_file_encoding="utf-8",
        case_sensitive=False,
        extra="ignore",
    )

# Create a global settings instance
settings = Settings()