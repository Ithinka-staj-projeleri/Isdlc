"""HTTP service around the agents, called only by the ISDLC backend (never by the browser).

    uvicorn isdlc_ai.server:app --port 8000

Endpoints:
    GET  /health                    is the service up, is a model configured
    POST /v1/meeting/suggestions    open issues and a minutes draft (type, participants, agenda, summary, next steps) from meeting notes
    POST /v1/risk/suggestions       risks found in a text, scored and with a preventive action (nothing is saved)
    POST /v1/risk/assessment        probability, impact and preventive action for one risk a person typed

The backend checks the user and the project, then sends only what the agent needs. The agent never
calls the ISDLC API or the database in this flow; the person reviews the suggestions in the form.
When AGENT_SERVICE_KEY is set, every request must carry it in the X-Agent-Key header.
"""

import hmac
import logging
from collections.abc import Callable
from datetime import date
from typing import TypeVar

from fastapi import Depends, FastAPI, Header, HTTPException
from pydantic import BaseModel, ConfigDict, Field
from pydantic.alias_generators import to_camel

from isdlc_ai.agents.base import AgentOutputError, RunInfo
from isdlc_ai.agents.meeting_agent import MeetingAgent, MeetingInput
from isdlc_ai.agents.risk_agent import RiskAgent, RiskInput
from isdlc_ai.config import Settings, load_settings
from isdlc_ai.llm import GeminiClient, LlmClient, LlmError

logger = logging.getLogger("isdlc_ai.server")

MAX_NOTES = 20_000
T = TypeVar("T")


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


class ParticipantDto(CamelModel):
    name: str
    role: str | None


class MinutesDraft(CamelModel):
    type: str  # MeetingType value
    type_reason: str | None
    participants: list[ParticipantDto]  # in the order the notes give them; role only when the notes say it
    agenda: str  # one "- " line per topic
    summary: str | None
    next_steps: str | None  # one "- " line per step
    warnings: list[str]


class MeetingSuggestionsResponse(CamelModel):
    summary: str
    items: list[SuggestedItem]
    # Items the model returned that did not pass validation; not shown, only counted.
    invalid_count: int
    run: RunDto
    # Type, participants, agenda, summary and next steps for the minutes form; null when that task failed
    # (the items above still count).
    minutes: MinutesDraft | None = None
    minutes_run: RunDto | None = None


class RiskSuggestionsRequest(CamelModel):
    project_code: str = Field(min_length=1, max_length=50)
    source_text: str = Field(min_length=1, max_length=MAX_NOTES)
    current_step: str | None = Field(default=None, max_length=1000)


class SuggestedRisk(CamelModel):
    title: str
    description: str | None
    category: str | None  # RiskCategory value, or null when the model gave none
    probability: int | None  # null when the model did not score it (see warnings)
    impact: int | None
    assessment_reason: str | None
    mitigation: str | None
    evidence: str | None
    warnings: list[str]


class RiskSuggestionsResponse(CamelModel):
    items: list[SuggestedRisk]
    invalid_count: int
    runs: list[RunDto]  # identify, assess, mitigate (only identify when no risk was found)


class RiskAssessmentRequest(CamelModel):
    project_code: str = Field(min_length=1, max_length=50)
    title: str = Field(min_length=1, max_length=300)
    description: str | None = Field(default=None, max_length=2000)
    current_step: str | None = Field(default=None, max_length=1000)


class RiskAssessmentResponse(CamelModel):
    probability: int | None
    impact: int | None
    assessment_reason: str | None
    mitigation: str | None
    warnings: list[str]
    runs: list[RunDto]  # assess, mitigate


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
        result = _run(lambda: agent.suggest_open_issues(meeting))

        # The minutes draft is a help on top: when it fails, the decisions and actions are still returned.
        minutes, minutes_run = None, None
        try:
            draft = agent.draft_minutes(meeting)
            minutes_run = _run_dto(draft.run)
            if draft.minutes is not None:
                minutes = MinutesDraft(
                    type=draft.minutes.type.value,
                    type_reason=draft.minutes.type_reason,
                    participants=[ParticipantDto(name=p.name, role=p.role) for p in draft.minutes.participants],
                    agenda=draft.minutes.agenda,
                    summary=draft.minutes.summary,
                    next_steps=draft.minutes.next_steps,
                    warnings=draft.warnings,
                )
            else:
                logger.info("Minutes draft not usable: %s", draft.invalid_reason)
        except (LlmError, AgentOutputError) as error:
            logger.warning("Minutes draft failed, returning the open issues only: %s", error)

        return MeetingSuggestionsResponse(
            minutes=minutes,
            minutes_run=minutes_run,
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
            run=_run_dto(result.run),
        )

    @app.post(
        "/v1/risk/suggestions",
        response_model=RiskSuggestionsResponse,
        response_model_by_alias=True,
        dependencies=[Depends(check_key)],
    )
    def risk_suggestions(request: RiskSuggestionsRequest) -> RiskSuggestionsResponse:
        agent = RiskAgent(make_llm())
        source = RiskInput(
            project_code=request.project_code,
            source_text=request.source_text,
            current_step=request.current_step,
        )
        result = _run(lambda: agent.run(source))

        return RiskSuggestionsResponse(
            items=[
                SuggestedRisk(
                    title=item.risk.title,
                    description=item.risk.description,
                    category=item.risk.category.value if item.risk.category else None,
                    probability=item.risk.probability,
                    impact=item.risk.impact,
                    assessment_reason=item.risk.assessment_reason,
                    mitigation=item.risk.mitigation,
                    evidence=item.risk.evidence,
                    warnings=item.warnings,
                )
                for item in result.items
            ],
            invalid_count=len(result.invalid),
            runs=[_run_dto(run) for run in result.runs],
        )

    @app.post(
        "/v1/risk/assessment",
        response_model=RiskAssessmentResponse,
        response_model_by_alias=True,
        dependencies=[Depends(check_key)],
    )
    def risk_assessment(request: RiskAssessmentRequest) -> RiskAssessmentResponse:
        agent = RiskAgent(make_llm())
        result = RiskAgent.from_risk(request.project_code, request.title, request.description, request.current_step)
        _run(lambda: agent.mitigate(agent.assess(result)))

        item = result.items[0]
        return RiskAssessmentResponse(
            probability=item.risk.probability,
            impact=item.risk.impact,
            assessment_reason=item.risk.assessment_reason,
            mitigation=item.risk.mitigation,
            warnings=item.warnings,
            runs=[_run_dto(run) for run in result.runs],
        )

    return app


def _run(call: Callable[[], T]) -> T:
    """Runs an agent; model failures become a 503 with a Turkish message the backend shows as is."""
    try:
        return call()
    except LlmError as error:
        logger.warning("Model call failed: %s (code %s)", error, error.code)
        raise HTTPException(503, _llm_error_message(error.code)) from error
    except AgentOutputError as error:
        logger.warning("Model output unusable: %s", error)
        raise HTTPException(503, "Model iki denemede de geçerli bir cevap vermedi. Tekrar deneyin.") from error


def _run_dto(run: RunInfo) -> RunDto:
    return RunDto(
        task=run.task,
        prompt_version=run.prompt_version,
        model_name=run.model_name,
        masked_count=run.masked_count,
        input_tokens=run.input_tokens,
        output_tokens=run.output_tokens,
    )


def _llm_error_message(code: int | None) -> str:
    """Shown to the user by the backend, so it is Turkish and has no technical detail."""
    if code == 429:
        return "Yapay zekâ servisinin kotası doldu. Biraz bekleyip tekrar deneyin."
    if code in (401, 403, 404):
        return "Yapay zekâ modeli kullanılamıyor; servis ayarlarının kontrol edilmesi gerekiyor."
    return "Yapay zekâ servisi şu an yoğun ya da ulaşılamıyor. Birkaç dakika sonra tekrar deneyin."


# For `uvicorn isdlc_ai.server:app`. Only reads ai/.env; the model is called per request.
app = create_app()
