"""Runs the risk agent (identify -> assess -> mitigate) on the sample cases and reports how close it gets.

    python evals/risk/run_eval.py            (from the ai/ folder; needs GEMINI_API_KEY)

Calls the real model (three calls per case), so it costs tokens. Cases contain only made-up data.
An expected risk counts as found when one of its keywords appears in a risk's title or description.
`minScore` checks that an obviously serious risk is not scored low. Extra risks are listed; read them,
they are not always wrong.
"""

import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from isdlc_ai.agents.base import total_tokens  # noqa: E402
from isdlc_ai.agents.risk_agent import RiskAgent, RiskInput, RiskItem  # noqa: E402
from isdlc_ai.config import load_settings  # noqa: E402
from isdlc_ai.llm import GeminiClient  # noqa: E402

CASES = Path(__file__).resolve().parent / "cases"


def tr_lower(text: str) -> str:
    return text.replace("I", "ı").replace("İ", "i").lower()


def text_of(item: RiskItem) -> str:
    return tr_lower(f"{item.risk.title} {item.risk.description or ''}")


def main() -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    settings = load_settings()
    if not settings.gemini_api_key or not settings.gemini_model:
        print("GEMINI_API_KEY ve GEMINI_MODEL ai/.env içinde tanımlı olmalı.")
        return 2

    agent = RiskAgent(GeminiClient(settings.gemini_api_key, settings.gemini_model))
    total_expected = total_found = 0

    for path in sorted(CASES.glob("*.json")):
        case = json.loads(path.read_text(encoding="utf-8"))
        result = agent.run(RiskInput(case["projectCode"], case["text"], case.get("sourceLabel", "Toplantı notları")))

        unused = list(result.items)
        found, missed, low_scores = [], [], []
        for expected in case["expected"]:
            keywords = [tr_lower(k) for k in expected["keywords"]]
            match = next((item for item in unused if any(k in text_of(item) for k in keywords)), None)
            if not match:
                missed.append(expected)
                continue
            unused.remove(match)
            found.append(expected)
            if "minScore" in expected and (match.risk.score or 0) < expected["minScore"]:
                low_scores.append(f"{match.risk.title}: skor {match.risk.score}, en az {expected['minScore']} bekleniyordu")

        total_expected += len(case["expected"])
        total_found += len(found)
        input_tokens, output_tokens = total_tokens(result.runs)

        print(f"\n## {case['name']}")
        print(f"Bulunan: {len(found)}/{len(case['expected'])}   Risk: {len(result.items)}   "
              f"Geçersiz: {len(result.invalid)}   Token: {input_tokens}/{output_tokens}")
        for expected in missed:
            print(f"  - Kaçırılan: {expected['keywords']}")
        for item in unused:
            print(f"  + Fazla: {item.risk.title}")
        for line in low_scores:
            print(f"  ! Düşük skor: {line}")
        for item in result.items:
            for warning in item.warnings:
                print(f"  ! {item.risk.title}: {warning}")

    recall = total_found / total_expected if total_expected else 0
    print(f"\nToplam: {total_found}/{total_expected} beklenen risk bulundu (%{recall:.0%}). Model: {settings.gemini_model}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
