# AI agent service (isdlc_ai/server.py). Built by docker-compose.yml: docker compose --profile ai up -d
FROM python:3.12-slim

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1

WORKDIR /app

# requirements-audio.txt adds speech to text for meeting recordings (faster-whisper) on top of requirements.txt.
COPY requirements.txt requirements-audio.txt ./
RUN pip install --no-cache-dir -r requirements-audio.txt

COPY isdlc_ai ./isdlc_ai

# Run as a normal user, not root.
# The Whisper model is downloaded on first use into this folder (a volume in docker-compose.yml, so it is kept).
RUN useradd --create-home agent && mkdir -p /home/agent/.cache/huggingface && chown -R agent /home/agent/.cache
USER agent

EXPOSE 8000
CMD ["uvicorn", "isdlc_ai.server:app", "--host", "0.0.0.0", "--port", "8000"]
