"""Risk agent: suggests risks for the Risks module.

Tasks (each is its own prompt and model call):
    identify   finds risks in a text (meeting notes, a document...)
    assess     suggests probability and impact (1-5) for each risk
    mitigate   suggests preventive actions for each risk

`run` chains all three. `assess` and `mitigate` also work on risks a person typed (`from_titles`),
so the same tasks can later back a "suggest a score" button in the Risks screen.
The score (probability × impact) is calculated in code, not by the model.
"""

from dataclasses import dataclass, field

from pydantic import BaseModel, Field, ValidationError

from isdlc_ai.agents.base import InvalidItem, RunInfo, Task, blank_to_none, found_in, run_task, short_reason
from isdlc_ai.llm import LlmClient
from isdlc_ai.schemas import RiskSuggestion


class LlmRisk(BaseModel):
    title: str
    description: str | None = None
    evidence: str


class IdentifyLlmOutput(BaseModel):
    items: list[LlmRisk]


class LlmAssessment(BaseModel):
    risk_no: int = Field(description="Number of the risk in the list")
    probability: int = Field(description="1-5")
    impact: int = Field(description="1-5")
    reason: str


class AssessLlmOutput(BaseModel):
    items: list[LlmAssessment]


class LlmMitigation(BaseModel):
    risk_no: int = Field(description="Number of the risk in the list")
    mitigation: str


class MitigateLlmOutput(BaseModel):
    items: list[LlmMitigation]


IDENTIFY = Task("risk.identify", "risk/identify.v1", IdentifyLlmOutput)
ASSESS = Task("risk.assess", "risk/assess.v1", AssessLlmOutput)
MITIGATE = Task("risk.mitigate", "risk/mitigate.v1", MitigateLlmOutput)


@dataclass
class RiskInput:
    project_code: str
    source_text: str
    source_label: str = "Toplantı notları"
    current_step: str | None = None


@dataclass
class RiskItem:
    risk: RiskSuggestion
    warnings: list[str] = field(default_factory=list)


@dataclass
class RiskResult:
    """Grows as the tasks run: identify fills the list, assess and mitigate fill in the fields."""

    project_code: str
    current_step: str | None
    source_text: str | None
    items: list[RiskItem]
    invalid: list[InvalidItem] = field(default_factory=list)
    runs: list[RunInfo] = field(default_factory=list)


class RiskAgent:
    def __init__(self, llm: LlmClient):
        self._llm = llm

    def run(self, source: RiskInput) -> RiskResult:
        """All three tasks in order. Assess and mitigate are skipped when no risk was found."""
        result = self.identify(source)
        if result.items:
            self.assess(result)
            self.mitigate(result)
        return result

    def identify(self, source: RiskInput) -> RiskResult:
        message = _header(source.project_code, source.current_step)
        message += ["", f"{source.source_label}:", '"""', source.source_text.strip(), '"""']
        task_run = run_task(self._llm, IDENTIFY, "\n".join(message))

        result = RiskResult(source.project_code, source.current_step, source.source_text, [], runs=[task_run.info])
        for raw in task_run.output.items:
            data = blank_to_none(raw.model_dump(), "description")
            try:
                risk = RiskSuggestion.model_validate(data)
            except ValidationError as error:
                result.invalid.append(InvalidItem(raw=data, reason=short_reason(error)))
                continue
            warnings = []
            if not risk.evidence or not found_in(risk.evidence, source.source_text):
                warnings.append("Dayanak cümle metinde birebir bulunamadı; riski metinle karşılaştırın.")
            result.items.append(RiskItem(risk, warnings))
        return result

    @staticmethod
    def from_titles(project_code: str, titles: list[str], current_step: str | None = None) -> RiskResult:
        """Risks a person typed, ready for assess and mitigate. No model call."""
        items = [RiskItem(RiskSuggestion(title=title.strip())) for title in titles if title.strip()]
        return RiskResult(project_code, current_step, None, items)

    def assess(self, result: RiskResult) -> RiskResult:
        task_run = run_task(self._llm, ASSESS, _risk_list_message(result, with_scores=False))
        result.runs.append(task_run.info)

        answers = {a.risk_no: a for a in task_run.output.items}
        for number, item in enumerate(result.items, start=1):
            answer = answers.get(number)
            if answer is None:
                item.warnings.append("Model bu riski puanlamadı.")
                continue
            try:
                item.risk = _with(item.risk, probability=answer.probability, impact=answer.impact,
                                  assessment_reason=answer.reason or None)
            except ValidationError:
                item.warnings.append(
                    f"Model geçersiz puan verdi (olasılık {answer.probability}, etki {answer.impact}); elle puanlanmalı."
                )
        return result

    def mitigate(self, result: RiskResult) -> RiskResult:
        task_run = run_task(self._llm, MITIGATE, _risk_list_message(result, with_scores=True))
        result.runs.append(task_run.info)

        answers = {a.risk_no: a for a in task_run.output.items}
        for number, item in enumerate(result.items, start=1):
            answer = answers.get(number)
            if answer is None or not answer.mitigation.strip():
                item.warnings.append("Model bu risk için önleyici aksiyon önermedi.")
                continue
            try:
                item.risk = _with(item.risk, mitigation=answer.mitigation.strip())
            except ValidationError as error:
                item.warnings.append(f"Önleyici aksiyon kullanılamadı: {short_reason(error)}")
        return result


def _with(risk: RiskSuggestion, **changes) -> RiskSuggestion:
    """Copy with changes, validated again (model_copy would skip the 1-5 and length checks)."""
    return RiskSuggestion.model_validate(risk.model_dump() | changes)


def _header(project_code: str, current_step: str | None) -> list[str]:
    lines = [f"Proje: {project_code}"]
    if current_step:
        lines.append(f"Projenin bulunduğu süreç adımı: {current_step}")
    return lines


def _risk_list_message(result: RiskResult, with_scores: bool) -> str:
    lines = _header(result.project_code, result.current_step) + ["", "Riskler:"]
    for number, item in enumerate(result.items, start=1):
        risk = item.risk
        lines.append(f"{number}. {risk.title}")
        if risk.description:
            lines.append(f"   Açıklama: {risk.description}")
        if risk.evidence:
            lines.append(f"   Dayanak: \"{risk.evidence}\"")
        if with_scores and risk.score is not None:
            lines.append(f"   Olasılık: {risk.probability}, Etki: {risk.impact}, Skor: {risk.score}")
    if result.source_text:
        lines += ["", "Risklerin çıkarıldığı metin (bağlam için):", '"""', result.source_text.strip(), '"""']
    return "\n".join(lines)
