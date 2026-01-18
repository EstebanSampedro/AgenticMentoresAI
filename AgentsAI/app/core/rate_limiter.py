import time
from typing import Dict, Tuple
from fastapi import FastAPI, Request, Response
from starlette.middleware.base import BaseHTTPMiddleware
from starlette.status import HTTP_429_TOO_MANY_REQUESTS

class RateLimiter(BaseHTTPMiddleware):
    """Rate limiting middleware to prevent abuse"""
    
    def __init__(self, app: FastAPI, max_requests: int = 100, window_size: int = 60):
        super().__init__(app)
        self.max_requests = max_requests  # Max requests per window
        self.window_size = window_size  # Window size in seconds
        self.clients: Dict[str, Tuple[int, float]] = {}  # IP -> (request_count, start_time)
    
    async def dispatch(self, request: Request, call_next):
        # Get client IP
        client_ip = request.client.host
        
        # Get current time
        current_time = time.time()
        
        # If client exists in our dict
        if client_ip in self.clients:
            req_count, start_time = self.clients[client_ip]
            
            # If window has expired, reset
            if current_time - start_time > self.window_size:
                self.clients[client_ip] = (1, current_time)
            else:
                # Increment request count
                req_count += 1
                self.clients[client_ip] = (req_count, start_time)
                
                # If too many requests, return 429
                if req_count > self.max_requests:
                    return Response(
                        content="Too many requests",
                        status_code=HTTP_429_TOO_MANY_REQUESTS,
                        headers={"Retry-After": str(self.window_size)}
                    )
        else:
            # First request from this client
            self.clients[client_ip] = (1, current_time)
        
        return await call_next(request)