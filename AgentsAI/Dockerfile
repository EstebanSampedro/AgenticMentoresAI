FROM python:3.13-slim

# Set working directory
WORKDIR /app

COPY . .


# Install system dependencies
# RUN apt-get update && apt-get install -y --no-install-recommends \
#     gcc \
#     && apt-get clean \
#     && rm -rf /var/lib/apt/lists/*

# Install Python dependencies
RUN pip install --no-cache-dir -r requirements.txt

# Create a non-root user
# RUN adduser --disabled-password --gecos "" appuser
# RUN chown -R appuser:appuser /app
# USER appuser

# Run the application
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000"]