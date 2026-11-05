"""HTTP service around the agents, called only by the ISDLC backend (never by the browser).

    uvicorn isdlc_ai.server:app --port 8000

Endpoints:
    GET  /health                    is the service up, is a model configured
    POST /v1/meeting/suggestions    open issue suggestions from meeting notes (nothing is saved)

The backend checks the user and the project, then sends only what the agent needs. The agent never
calls the ISDLC API or the database in this flow; the person reviews the suggestions in the meeting form.
When AGENT_SERVICE_KEY is set, every request must carry it in the X-Agent-Key header.
"""

import hmac
import logging
from collections.abc import Callable
from datetime import date

from fastapi import Depends, FastAPI, Header, HTTPException
from pydantic import BaseModel, ConfigDict, Field
from pydantic.alias_generators import to_camel

from isdlc_ai.agents.base import AgentOutputError
from isdlc_ai.agents.meeting_agent import MeetingAgent, MeetingInput
from isdlc_ai.config import Settings, load_settings
from isdlc_ai.llm import GeminiClient, LlmClient, LlmError

logger = logging.getLogger("isdlc_ai.server")

MAX_NOTES = 20_000


class CamelModel(BaseModel):
    """JSON in camelCase, like the ISDLC API."""

    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True)


class MeetingSuggestionsRequest(CamelModel):
    project_code: str = Field(min_length=1, max_length=50)
    meeting_date: date
    notes: str = Field(min_length=1, max_length=MAX_NOTES)
    current_step: str | None = Field(default=None, max_length=1000)


class SuggestedItem(CamelModel):
    type: str
    title: str
    description: str | None
    owner_name: str | None
    due_date: date | None
    evidence: str
    warnings: list[str]


class RunDto(CamelModel):
    task: str
    prompt_version: str
    model_name: str
    masked_count: int
    input_tokens: int | None
    output_tokens: int | None


class MeetingSuggestionsResponse(CamelModel):
    summary: str
    items: list[SuggestedItem]
    # Items the model returned that did not pass validation; not shown, only counted.
    invalid_count: int
    run: RunDto


def create_app(settings: Settings | None = None, llm_factory: Callable[[], LlmClient] | None = None) -> FastAPI:
    """llm_factory lets tests use FakeLlmClient instead of the real model."""
    settings = settings or load_settings()
    model_configured = bool(settings.gemini_api_key and settings.gemini_model)

    def default_llm() -> LlmClient:
        if not model_configured:
            raise HTTPException(503, "Yapay zekâ modeli yapılandırılmamış (GEMINI_API_KEY, GEMINI_MODEL).")
        return GeminiClient(settings.gemini_api_key, settings.gemini_model)

    make_llm = llm_factory or default_llm

    def check_key(x_agent_key: str | None = Header(default=None)) -> None:
        expected = settings.agent_service_key
        if expected and not (x_agent_key and hmac.compare_digest(x_agent_key, expected)):
            raise HTTPException(401, "Geçersiz servis anahtarı.")

    app = FastAPI(title="ISDLC AI agents", version="1")

    @app.get("/health")
    def health() -> dict:
        return {"status": "ok", "modelConfigured": model_configured or llm_factory is not None}

    @app.post(
        "/v1/meeting/suggestions",
        response_model=MeetingSuggestionsResponse,
        response_model_by_alias=True,
        dependencies=[Depends(check_key)],
    )
    def meeting_suggestions(request: MeetingSuggestionsRequest) -> MeetingSuggestionsResponse:
        agent = MeetingAgent(make_llm())
        meeting = MeetingInput(
            project_code=request.project_code,
            meeting_date=request.meeting_date,
            notes=request.notes,
            current_step=request.current_step,
        )
        try:
            result = agent.suggest_open_issues(meeting)
        except LlmError as error:
            logger.warning("Model call failed: %s (code %s)", error, error.code)
            raise HTTPException(503, _llm_error_message(error.code)) from error
        except AgentOutputError as error:
            logger.warning("Model output unusable: %s", error)
            raise HTTPException(503, "Model iki denemede de geçerli bir cevap vermedi. Tekrar deneyin.") from error

        return MeetingSuggestionsResponse(
            summary=result.summary,
            items=[
                SuggestedItem(
                    type=s.item.type.value,
                    title=s.item.title,
                    description=s.item.description,
                    owner_name=s.item.owner_name,
                    due_date=s.item.due_date,
                    evidence=s.item.evidence,
                    warnings=s.warnings,
                )
                for s in result.suggestions
            ],
            invalid_count=len(result.invalid),
            run=RunDto(
                task=result.run.task,
                prompt_version=result.run.prompt_version,
                model_name=result.run.model_name,
                masked_count=result.run.masked_count,
                input_tokens=result.run.input_tokens,
                output_tokens=result.run.output_tokens,
            ),
        )

    return app


def _llm_error_message(code: int | None) -> str:
    """Shown to the user by the backend, so it is Turkish and has no technical detail."""
    if code == 429:
        return "Yapay zekâ servisinin kotası doldu. Biraz bekleyip tekrar deneyin."
    if code in (401, 403, 404):
        return "Yapay zekâ modeli kullanılamıyor; servis ayarlarının kontrol edilmesi gerekiyor."
    return "Yapay zekâ servisi şu an yoğun ya da ulaşılamıyor. Birkaç dakika sonra tekrar deneyin."


# For `uvicorn isdlc_ai.server:app`. Only reads ai/.env; the model is called per request.
app = create_app()
