import os
from openai import OpenAI
from typing import Optional
from app.core.config import settings

class OpenAIClient:
    def __init__(self, api_key: Optional[str] = None):
        """
        Initialize OpenAI client
        
        Args:
            api_key: OpenAI API key. If None, will try to get from environment variable
        """
        self.api_key = settings.openai_api_key
        if not self.api_key:
            raise ValueError("OpenAI API key is required. Set OPENAI_API_KEY environment variable or pass api_key parameter.")
        
        self.client = OpenAI(api_key=self.api_key)
    
    async def vector_search(self, query: str, vector_store_id: str, max_num_results: int = 2) -> list:
        """
        Perform vector search
        
        Args:
            query: Search query
            vector_store_id: ID of the vector store to search in
            top_k: Number of top results to return
            
        Returns:
            List of search results
        """
        try:
            response = self.client.vector_stores.search(
                query=query,
                vector_store_id=vector_store_id,
                max_num_results=max_num_results
            )
            return response.data
        except Exception as e:
            raise Exception(f"Error performing vector search: {str(e)}")


# Singleton instance
openai_client = None

def get_openai_client() -> OpenAIClient:
    """Get or create OpenAI client instance"""
    global openai_client
    if openai_client is None:
        openai_client = OpenAIClient()
    return openai_client