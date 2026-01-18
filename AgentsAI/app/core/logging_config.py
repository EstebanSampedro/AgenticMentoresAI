import logging
from logging.config import dictConfig
import os
from pathlib import Path


class LoggingConfig:
    """Logging configuration for the application."""
    
    @staticmethod
    def setup_logging(log_dir=None):
        """Setup logging configuration.
        
        Args:
            log_dir (str, optional): Directory to store log files. Defaults to current directory.
        
        Returns:
            Logger: Configured logger instance.
        """
        # Get log level and other parameters from environment variables
        log_level = os.environ.get("LOG_LEVEL", "INFO")
        log_filename = os.environ.get("LOG_FILENAME", "app.log")
        max_bytes = int(os.environ.get("LOG_MAX_BYTES", 10485760))  # 10 MB
        backup_count = int(os.environ.get("LOG_BACKUP_COUNT", 5))
        
        # Ensure log directory exists
        if log_dir:
            log_path = Path(log_dir)
            log_path.mkdir(parents=True, exist_ok=True)
            log_file = str(log_path / log_filename)
        else:
            log_file = log_filename
            
        # Configure log format based on environment
        is_development = os.environ.get("ENVIRONMENT", "development").lower() == "development"
        log_format = "%(asctime)s - %(name)s - %(levelname)s - %(module)s:%(lineno)d - %(message)s" if is_development else "%(asctime)s - %(name)s - %(levelname)s - %(message)s"
        
        log_config = {
            "version": 1,
            "disable_existing_loggers": False,
            "formatters": {
                "default": {
                    "format": log_format,
                    "datefmt": "%Y-%m-%d %H:%M:%S",
                },
                "json": {
                    "()": "pythonjsonlogger.jsonlogger.JsonFormatter",
                    "format": "%(asctime)s %(name)s %(levelname)s %(module)s %(lineno)d %(message)s",
                    "datefmt": "%Y-%m-%d %H:%M:%S",
                },
            },
            "handlers": {
                "console": {
                    "class": "logging.StreamHandler",
                    "level": log_level,
                    "formatter": "default",
                },
                "file": {
                    "class": "logging.handlers.RotatingFileHandler",
                    "level": log_level,
                    "formatter": "default",
                    "filename": log_file,
                    "maxBytes": max_bytes,
                    "backupCount": backup_count,
                    "encoding": "utf8",
                },
            },
            "loggers": {
                "app": {
                    "level": log_level,
                    "handlers": ["console", "file"],
                    "propagate": False,
                },
                "uvicorn": {
                    "level": log_level,
                    "handlers": ["console", "file"],
                    "propagate": False,
                },
                # Add more specific loggers as needed
                "fastapi": {
                    "level": log_level,
                    "handlers": ["console", "file"],
                    "propagate": False,
                },
            },
            "root": {
                "level": log_level,
                "handlers": ["console", "file"],
            },
        }
        
        # If JSON logging is enabled and python-json-logger is installed
        if os.environ.get("JSON_LOGGING", "false").lower() == "true":
            try:
                import pythonjsonlogger
                for handler in log_config["handlers"].values():
                    if "formatter" in handler:
                        handler["formatter"] = "json"
            except ImportError:
                logging.warning("python-json-logger not installed, using default formatter")
        
        dictConfig(log_config)
        return logging.getLogger("app")