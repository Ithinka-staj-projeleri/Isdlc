# AI agent service (isdlc_ai/server.py). Built by docker-compose.yml: docker compose --profile ai up -d
FROM python:3.12-slim

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1

WORKDIR /app

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY isdlc_ai ./isdlc_ai

# Run as a normal user, not root.
RUN useradd --create-home agent
USER agent

EXPOSE 8000
CMD ["uvicorn", "isdlc_ai.server:app", "--host", "0.0.0.0", "--port", "8000"]
