"""Runs the meeting agent on the sample cases and reports how close it gets.

    python evals/meeting/run_eval.py            (from the ai/ folder; needs GEMINI_API_KEY)

Calls the real model, so it costs tokens. Cases contain only made-up data.
An expected item counts as found when a suggestion has the same type and one of the keywords
appears in its title or description. Suggestions that match nothing are reported as extra;
they are not always wrong, so read them.
"""

import json
import sys
from datetime import date
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from isdlc_ai.agents.meeting_agent import MeetingAgent, MeetingInput, Suggestion  # noqa: E402
from isdlc_ai.config import load_settings  # noqa: E402
from isdlc_ai.llm import GeminiClient  # noqa: E402

CASES = Path(__file__).resolve().parent / "cases"


def tr_lower(text: str) -> str:
    return text.replace("I", "ı").replace("İ", "i").lower()


def text_of(suggestion: Suggestion) -> str:
    return tr_lower(f"{suggestion.item.title} {suggestion.item.description or ''}")


def evaluate(case: dict, suggestions: list[Suggestion]) -> dict:
    unused = list(suggestions)
    found, missed = [], []
    for expected in case["expected"]:
        keywords = [tr_lower(k) for k in expected["keywords"]]
        match = next(
            (s for s in unused if s.item.type.value == expected["type"] and any(k in text_of(s) for k in keywords)),
            None,
        )
        if match:
            unused.remove(match)
            found.append(expected)
        else:
            missed.append(expected)

    wrong_dates = []
    for keyword, expected_date in case.get("expectedDueDates", {}).items():
        match = next((s for s in suggestions if tr_lower(keyword) in text_of(s)), None)
        if match and str(match.item.due_date) != expected_date:
            wrong_dates.append(f"{keyword}: beklenen {expected_date}, gelen {match.item.due_date}")

    return {"found": found, "missed": missed, "extra": unused, "wrong_dates": wrong_dates}


def main() -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    settings = load_settings()
    if not settings.gemini_api_key or not settings.gemini_model:
        print("GEMINI_API_KEY ve GEMINI_MODEL ai/.env içinde tanımlı olmalı.")
        return 2

    agent = MeetingAgent(GeminiClient(settings.gemini_api_key, settings.gemini_model))
    total_expected = total_found = total_suggestions = total_invalid = 0

    for path in sorted(CASES.glob("*.json")):
        case = json.loads(path.read_text(encoding="utf-8"))
        result = agent.suggest_open_issues(
            MeetingInput(
                project_code=case["projectCode"],
                meeting_date=date.fromisoformat(case["meetingDate"]),
                notes=case["notes"],
            )
        )
        report = evaluate(case, result.suggestions)

        total_expected += len(case["expected"])
        total_found += len(report["found"])
        total_suggestions += len(result.suggestions)
        total_invalid += len(result.invalid)

        print(f"\n## {case['name']}")
        print(f"Bulunan: {len(report['found'])}/{len(case['expected'])}   Öneri: {len(result.suggestions)}   Geçersiz: {len(result.invalid)}")
        for expected in report["missed"]:
            print(f"  - Kaçırılan: {expected['type']} {expected['keywords']}")
        for suggestion in report["extra"]:
            print(f"  + Fazla: {suggestion.item.type.value} — {suggestion.item.title}")
        for line in report["wrong_dates"]:
            print(f"  ! Termin: {line}")
        for suggestion in result.suggestions:
            for warning in suggestion.warnings:
                if "Dayanak" in warning:
                    print(f"  ! Dayanak bulunamadı: {suggestion.item.title}")

    recall = total_found / total_expected if total_expected else 0
    print(f"\nToplam: {total_found}/{total_expected} beklenen kayıt bulundu (%{recall:.0%}), "
          f"{total_suggestions} öneri, {total_invalid} geçersiz. Model: {settings.gemini_model}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
