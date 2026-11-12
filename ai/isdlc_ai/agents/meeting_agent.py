"""Meeting agent: works on meeting notes and suggests what the Meetings module records.

Tasks (each is its own prompt and model call):
    suggest_open_issues   actions, decisions, questions... for the Open Issues list
    draft_minutes         meeting type, participants with roles, agenda, summary and next steps for the minutes

The agent never saves anything. A person reviews the suggestions; only accepted ones go to the API.
"""

from dataclasses import dataclass, field
from datetime import date

from pydantic import BaseModel, Field, ValidationError

from isdlc_ai.agents.base import InvalidItem, RunInfo, Task, blank_to_none, found_in, run_task, short_reason, weekday_tr
from isdlc_ai.llm import LlmClient
from isdlc_ai.schemas import MeetingMinutesSuggestion, OpenIssueSuggestion, OpenIssueType


# What the model is asked to return. Deliberately loose: every item is validated on its own
# afterwards, so one bad item does not throw away the whole answer.
class LlmOpenIssue(BaseModel):
    type: str = Field(description="One of: Action, Decision, Question, Dependency, Assumption, ChangeRequest")
    title: str
    description: str | None = None
    owner_name: str | None = None
    due_date: str | None = Field(default=None, description="YYYY-MM-DD")
    evidence: str


class OpenIssuesLlmOutput(BaseModel):
    summary: str
    items: list[LlmOpenIssue]


class LlmParticipant(BaseModel):
    name: str
    role: str | None = Field(default=None, description="Role, title, unit or company; only when the notes say it")


class MinutesLlmOutput(BaseModel):
    meeting_type: str = Field(
        description="One of: RequestInterview, AnalysisSession, KickOff, Review, Internal, CustomerVisit, Handover, ProjectEvaluation"
    )
    type_reason: str
    participants: list[LlmParticipant]
    agenda: list[str]
    summary: str = ""
    next_steps: list[str] = Field(default_factory=list)


OPEN_ISSUES = Task("meeting.open_issues", "meeting/open_issues.v1", OpenIssuesLlmOutput)
MINUTES = Task("meeting.minutes", "meeting/minutes.v2", MinutesLlmOutput)


@dataclass
class MeetingInput:
    project_code: str
    meeting_date: date
    notes: str
    current_step: str | None = None  # e.g. "7.1 Paydaşlarla analiz toplantıları yapılır..."


@dataclass
class Suggestion:
    item: OpenIssueSuggestion
    warnings: list[str] = field(default_factory=list)


@dataclass
class OpenIssuesResult:
    summary: str
    suggestions: list[Suggestion]
    invalid: list[InvalidItem]
    run: RunInfo

    def of_type(self, issue_type: OpenIssueType) -> list[OpenIssueSuggestion]:
        return [s.item for s in self.suggestions if s.item.type == issue_type]


@dataclass
class MinutesResult:
    minutes: MeetingMinutesSuggestion | None  # None when the answer failed validation
    warnings: list[str]
    run: RunInfo
    invalid_reason: str | None = None


class MeetingAgent:
    def __init__(self, llm: LlmClient):
        self._llm = llm

    def suggest_open_issues(self, meeting: MeetingInput) -> OpenIssuesResult:
        task_run = run_task(self._llm, OPEN_ISSUES, _user_message(meeting))

        suggestions: list[Suggestion] = []
        invalid: list[InvalidItem] = []
        for raw in task_run.output.items:
            data = blank_to_none(raw.model_dump(), "description", "owner_name", "due_date")
            try:
                item = OpenIssueSuggestion.model_validate(data)
            except ValidationError as error:
                invalid.append(InvalidItem(raw=data, reason=short_reason(error)))
                continue
            suggestions.append(Suggestion(item=item, warnings=_open_issue_warnings(item, meeting)))

        return OpenIssuesResult(task_run.output.summary, suggestions, invalid, task_run.info)

    def draft_minutes(self, meeting: MeetingInput) -> MinutesResult:
        task_run = run_task(self._llm, MINUTES, _user_message(meeting))
        output = task_run.output

        # A blank role is no role; a minutes draft without participants fails validation (the form needs one).
        participants = [
            {"name": p.name.strip(), "role": (p.role or "").strip() or None}
            for p in output.participants
            if p.name.strip()
        ]
        agenda = [a.strip() for a in output.agenda if a.strip()]
        next_steps = [n.strip() for n in output.next_steps if n.strip()]
        try:
            minutes = MeetingMinutesSuggestion(
                type=output.meeting_type,
                date=meeting.meeting_date,
                participants=participants,
                agenda="\n".join(f"- {topic}" for topic in agenda),
                summary=output.summary.strip() or None,
                next_steps="\n".join(f"- {step}" for step in next_steps) or None,
                type_reason=output.type_reason or None,
            )
        except ValidationError as error:
            return MinutesResult(None, [], task_run.info, invalid_reason=short_reason(error))

        warnings = [
            f"Katılımcı notlarda geçmiyor: {p.name}" for p in minutes.participants if not found_in(p.name, meeting.notes)
        ] + [
            f"Rol notlarda geçmiyor: {p.name} ({p.role})"
            for p in minutes.participants
            if p.role and not found_in(p.role, meeting.notes)
        ]
        return MinutesResult(minutes, warnings, task_run.info)


def _user_message(meeting: MeetingInput) -> str:
    """Both tasks read the same notes; only the system prompt (the task) differs."""
    lines = [
        f"Proje: {meeting.project_code}",
        f"Toplantı tarihi: {meeting.meeting_date.isoformat()} ({weekday_tr(meeting.meeting_date)})",
    ]
    if meeting.current_step:
        lines.append(f"Projenin bulunduğu süreç adımı: {meeting.current_step}")
    lines += ["", "Toplantı notları:", '"""', meeting.notes.strip(), '"""']
    return "\n".join(lines)


def _open_issue_warnings(item: OpenIssueSuggestion, meeting: MeetingInput) -> list[str]:
    warnings = []
    if not found_in(item.evidence, meeting.notes):
        warnings.append("Dayanak cümle notlarda birebir bulunamadı; öneriyi notlarla karşılaştırın.")
    if item.due_date and item.due_date < meeting.meeting_date:
        warnings.append("Termin toplantı tarihinden önce.")
    if not item.owner_name:
        warnings.append("Sorumlu belirtilmemiş; kaydetmeden önce sorumlu girilmeli.")
    return warnings
