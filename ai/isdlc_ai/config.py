import os
from dataclasses import dataclass
from pathlib import Path

from dotenv import load_dotenv

AI_ROOT = Path(__file__).resolve().parent.parent


@dataclass(frozen=True)
class Settings:
    isdlc_api_url: str
    gemini_api_key: str | None
    gemini_model: str | None
    # Shared with the backend (Agents:ServiceKey); when set, the HTTP service requires it.
    agent_service_key: str | None = None


def load_settings() -> Settings:
    load_dotenv(AI_ROOT / ".env")
    return Settings(
        isdlc_api_url=os.getenv("ISDLC_API_URL", "http://localhost:5091").rstrip("/"),
        gemini_api_key=os.getenv("GEMINI_API_KEY") or None,
        gemini_model=os.getenv("GEMINI_MODEL") or None,
        agent_service_key=os.getenv("AGENT_SERVICE_KEY") or None,
    )
