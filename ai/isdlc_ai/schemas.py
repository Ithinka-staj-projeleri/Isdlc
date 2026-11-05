"""Shapes shared with the ISDLC API. Keep in line with docs/open-issues-meetings-api.md."""

from datetime import date
from enum import StrEnum

from pydantic import BaseModel, Field, computed_field


class OpenIssueType(StrEnum):
    QUESTION = "Question"
    ACTION = "Action"
    DECISION = "Decision"
    CHANGE_REQUEST = "ChangeRequest"
    DEPENDENCY = "Dependency"
    ASSUMPTION = "Assumption"


class OpenIssueSuggestion(BaseModel):
    """An open issue the agent suggests. Same limits as CreateOpenIssueDto, plus the evidence."""

    type: OpenIssueType
    title: str = Field(min_length=1, max_length=300)
    description: str | None = Field(default=None, max_length=2000)
    owner_name: str | None = Field(default=None, max_length=200)
    due_date: date | None = None
    evidence: str = Field(min_length=1, description="Quote from the meeting notes the suggestion is based on")

    def to_create_dto(self, owner_name: str | None = None) -> dict:
        """Body for POST /api/v1/projects/{projectId}/open-issues."""
        return {
            "type": self.type.value,
            "title": self.title,
            "description": self.description,
            "ownerUserId": None,
            "ownerName": owner_name or self.owner_name,
            "dueDate": self.due_date.isoformat() if self.due_date else None,
        }


class MeetingType(StrEnum):
    REQUEST_INTERVIEW = "RequestInterview"
    ANALYSIS_SESSION = "AnalysisSession"
    KICK_OFF = "KickOff"
    REVIEW = "Review"
    INTERNAL = "Internal"
    CUSTOMER_VISIT = "CustomerVisit"
    HANDOVER = "Handover"
    PROJECT_EVALUATION = "ProjectEvaluation"


class MeetingMinutesSuggestion(BaseModel):
    """Meeting minutes the agent drafts. Same limits as CreateMeetingDto (docs/open-issues-meetings-api.md)."""

    type: MeetingType
    date: date
    participants: str = Field(min_length=1, max_length=1000)
    agenda: str = Field(min_length=1, max_length=4000)
    type_reason: str | None = Field(default=None, description="Why the model chose this meeting type")

    def to_create_dto(self, decisions: list[OpenIssueSuggestion], actions: list[OpenIssueSuggestion]) -> dict:
        """Body for POST /api/v1/projects/{projectId}/meetings. Decisions and actions come from the open issues task."""
        return {
            "type": self.type.value,
            "date": self.date.isoformat(),
            "participants": self.participants,
            "agenda": self.agenda,
            "decisions": [decision.title for decision in decisions],
            "actions": [
                {
                    "title": action.title,
                    "ownerUserId": None,
                    "ownerName": action.owner_name,
                    "dueDate": action.due_date.isoformat() if action.due_date else None,
                }
                for action in actions
            ],
        }


class RiskSuggestion(BaseModel):
    """A risk the agent suggests.

    The Risks module has no API contract yet, so the field limits and the 1-5 scale are assumptions
    to be aligned with the Risks contract when it is written.
    """

    title: str = Field(min_length=1, max_length=300)
    description: str | None = Field(default=None, max_length=2000)
    evidence: str | None = Field(default=None, description="Quote from the source text; empty for risks a person typed")
    probability: int | None = Field(default=None, ge=1, le=5)
    impact: int | None = Field(default=None, ge=1, le=5)
    assessment_reason: str | None = Field(default=None, max_length=1000)
    mitigation: str | None = Field(default=None, max_length=2000)

    @computed_field
    @property
    def score(self) -> int | None:
        """Probability × impact. Calculated here, never asked from the model."""
        if self.probability is None or self.impact is None:
            return None
        return self.probability * self.impact
