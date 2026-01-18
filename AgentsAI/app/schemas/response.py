from typing import Any, Optional
from pydantic import BaseModel

class APIResponse(BaseModel):
    success: bool
    code: int
    message: str
    data: Optional[Any] = None
    errors: Optional[Any] = None
    meta: Optional[Any] = None
