"""Request agent: fills the Talep Kayıt Formu from the e-mail a request came with.

Task:
    draft_from_email   name, description, customer, requester and contact, type, dates and notes

The agent never saves anything. The form fills only the fields the person has not filled; the person saves.
"""

from dataclasses import dataclass, field
from datetime import date

from pydantic import BaseModel, Field

from isdlc_ai.agents.base import RunInfo, Task, run_task, weekday_tr
from isdlc_ai.llm import LlmClient
from isdlc_ai.masking import PLACEHOLDER

REQUEST_TYPES = {"NewProject", "AdditionalModule", "CompanyProduct", "Support"}

# The request form's limits (docs/projects-api.md); a longer value is dropped, not cut.
MAX_NAME = 200
MAX_TEXT = 2000


class RequestLlmOutput(BaseModel):
    name: str | None = None
    description: str | None = None
    customer_name: str | None = None
    requester_name: str | None = None
    requester_contact: str | None = None
    request_type: str | None = Field(default=None, description="One of: NewProject, AdditionalModule, CompanyProduct, Support")
    request_date: str | None = Field(default=None, description="YYYY-MM-DD")
    due_date: str | None = Field(default=None, description="YYYY-MM-DD")
    notes: str | None = None
    warnings: list[str] = Field(default_factory=list)


FROM_EMAIL = Task("request.from_email", "request/from_email.v1", RequestLlmOutput)


@dataclass
class RequestDraft:
    name: str | None = None
    description: str | None = None
    customer_name: str | None = None
    requester_name: str | None = None
    requester_contact: str | None = None
    request_type: str | None = None
    request_date: date | None = None
    due_date: date | None = None
    notes: str | None = None
    warnings: list[str] = field(default_factory=list)


@dataclass
class RequestDraftResult:
    draft: RequestDraft
    run: RunInfo


class RequestAgent:
    def __init__(self, llm: LlmClient):
        self._llm = llm

    def draft_from_email(self, email_text: str, today: date) -> RequestDraftResult:
        message = "\n".join([
            f"Bugünün tarihi: {today.isoformat()} ({weekday_tr(today)})",
            "",
            "Talep e-postası:",
            '"""',
            email_text.strip(),
            '"""',
        ])
        task_run = run_task(self._llm, FROM_EMAIL, message)
        return RequestDraftResult(_checked(task_run.output), task_run.info)


def _checked(output: RequestLlmOutput) -> RequestDraft:
    """Keeps what the form can take; every dropped value becomes a warning so the person knows to look."""
    warnings = [w.strip() for w in output.warnings if w and w.strip()]

    def text(value: str | None, limit: int, label: str) -> str | None:
        value = (value or "").strip()
        if not value:
            return None
        if len(value) > limit:
            warnings.append(f"{label} {limit} karakterden uzun olduğu için doldurulmadı.")
            return None
        return value

    def day(value: str | None, label: str) -> date | None:
        if not value or not value.strip():
            return None
        try:
            return date.fromisoformat(value.strip())
        except ValueError:
            warnings.append(f"{label} anlaşılamadı; elle girin.")
            return None

    request_type = (output.request_type or "").strip() or None
    if request_type is not None and request_type not in REQUEST_TYPES:
        warnings.append("Talep türü anlaşılamadı; elle seçin.")
        request_type = None

    contact = text(output.requester_contact, MAX_NAME, "İletişim bilgisi")
    if contact is not None and PLACEHOLDER.search(contact):
        # A placeholder left after unmasking was made up by the model: the e-mail had no such value.
        warnings.append("İletişim bilgisi e-postada bulunamadı; elle girin.")
        contact = None

    request_date = day(output.request_date, "Talep tarihi")
    due_date = day(output.due_date, "Bitiş tarihi")
    if request_date and due_date and due_date < request_date:
        warnings.append("Önerilen bitiş tarihi talep tarihinden önce olduğu için doldurulmadı.")
        due_date = None

    return RequestDraft(
        name=text(output.name, MAX_NAME, "Proje adı"),
        description=text(output.description, MAX_TEXT, "Açıklama"),
        customer_name=text(output.customer_name, MAX_NAME, "Talep sahibi"),
        requester_name=text(output.requester_name, MAX_NAME, "Talebi ileten kişi"),
        requester_contact=contact,
        request_type=request_type,
        request_date=request_date,
        due_date=due_date,
        notes=text(output.notes, MAX_TEXT, "Notlar"),
        warnings=warnings,
    )
