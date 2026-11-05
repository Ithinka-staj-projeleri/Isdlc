"""Parts every agent task shares: prompt loading, masking, the model call with one retry, small checks.

An agent is a group of tasks that suggest the same kind of record (open issues and minutes, risks...).
A task is one prompt file plus the JSON shape the model must return. The agent builds the user message
and adds its own checks; everything else is here, so a new task is mostly a new prompt and a new shape.
"""

import re
from dataclasses import dataclass
from datetime import date
from pathlib import Path
from typing import Generic, TypeVar

from pydantic import BaseModel, ValidationError

from isdlc_ai.llm import LlmClient
from isdlc_ai.masking import MaskedText, mask

PROMPTS = Path(__file__).resolve().parent.parent / "prompts"

T = TypeVar("T", bound=BaseModel)


class AgentOutputError(Exception):
    """The model did not return usable JSON, even after one retry."""


@dataclass(frozen=True)
class Task(Generic[T]):
    name: str  # "risk.assess"
    prompt_version: str  # file under prompts/ without ".md": "risk/assess.v1"
    output: type[T]  # JSON shape the model must return

    def system_prompt(self) -> str:
        return (PROMPTS / f"{self.prompt_version}.md").read_text(encoding="utf-8")


@dataclass
class RunInfo:
    """What was used for one model call; saved with every result so answers can be traced."""

    task: str
    prompt_version: str
    model_name: str
    masked_count: int
    input_tokens: int | None = None
    output_tokens: int | None = None


@dataclass
class TaskRun(Generic[T]):
    output: T  # personal data already restored
    info: RunInfo


@dataclass
class InvalidItem:
    raw: dict
    reason: str


def run_task(llm: LlmClient, task: Task[T], user_message: str) -> TaskRun[T]:
    """Masks the message, asks the model, retries once on broken JSON, restores the masked values."""
    masked = mask(user_message)
    system = task.system_prompt()

    first = llm.generate_json(system, masked.text, task.output)
    input_tokens, output_tokens = first.input_tokens, first.output_tokens
    try:
        output = task.output.model_validate_json(first.text)
    except ValidationError as error:
        retry_message = (
            f"{masked.text}\n\nÖnceki cevabın şemaya uymadı ({short_reason(error)}). "
            "Yalnızca şemaya uyan geçerli JSON döndür."
        )
        retry = llm.generate_json(system, retry_message, task.output)
        input_tokens, output_tokens = _sum(input_tokens, retry.input_tokens), _sum(output_tokens, retry.output_tokens)
        try:
            output = task.output.model_validate_json(retry.text)
        except ValidationError as retry_error:
            raise AgentOutputError(f"{task.name}: {short_reason(retry_error)}") from retry_error

    restored = task.output.model_validate(_unmask_values(output.model_dump(), masked))
    info = RunInfo(task.name, task.prompt_version, llm.model_name, len(masked.replacements), input_tokens, output_tokens)
    return TaskRun(restored, info)


def blank_to_none(data: dict, *keys: str) -> dict:
    for key in keys:
        if isinstance(data.get(key), str) and not data[key].strip():
            data[key] = None
    return data


def found_in(quote: str, source: str) -> bool:
    """True if the quote appears in the source, ignoring case, punctuation and extra spaces."""
    return _normalize(quote) in _normalize(source)


def short_reason(error: ValidationError) -> str:
    parts = []
    for issue in error.errors()[:3]:
        location = ".".join(str(part) for part in issue["loc"]) or "cevap"
        parts.append(f"{location}: {issue['msg']}")
    return "; ".join(parts)


def total_tokens(runs: list[RunInfo]) -> tuple[int | None, int | None]:
    input_tokens = output_tokens = None
    for run in runs:
        input_tokens, output_tokens = _sum(input_tokens, run.input_tokens), _sum(output_tokens, run.output_tokens)
    return input_tokens, output_tokens


_WEEKDAYS_TR = ["Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi", "Pazar"]


def weekday_tr(value: date) -> str:
    return _WEEKDAYS_TR[value.weekday()]


def _unmask_values(value, masked: MaskedText):
    if isinstance(value, str):
        return masked.unmask(value)
    if isinstance(value, list):
        return [_unmask_values(item, masked) for item in value]
    if isinstance(value, dict):
        return {key: _unmask_values(item, masked) for key, item in value.items()}
    return value


def _normalize(text: str) -> str:
    text = text.replace("I", "ı").replace("İ", "i").lower()
    text = re.sub(r"[\"'“”‘’.,;:!?()\-]", " ", text)
    return re.sub(r"\s+", " ", text).strip()


def _sum(a: int | None, b: int | None) -> int | None:
    return None if a is None and b is None else (a or 0) + (b or 0)
