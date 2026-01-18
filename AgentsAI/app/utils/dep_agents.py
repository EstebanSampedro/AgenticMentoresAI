# app/utils/dep_agents.py

from typing import Callable, Awaitable
from app.agents.manager_agent import run_manager

def get_manager() -> Callable[[str], Awaitable[str]]:
    """
    Dependencia que provee la función para ejecutar el Manager.
    """
    return run_manager
