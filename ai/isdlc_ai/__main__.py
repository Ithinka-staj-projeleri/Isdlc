"""Command line entry point.

    python -m isdlc_ai meeting --project-id 3 --meeting-date 2026-11-04 --notes notes.txt [--minutes] [--apply]
    python -m isdlc_ai risks --project-id 3 --notes notes.txt
    python -m isdlc_ai risks --project-code ISDLC-2026-014 --risk-list risks.txt
    python -m isdlc_ai models

Without --apply nothing is written to ISDLC. With --apply every open issue suggestion is shown and only
the ones you accept are created (as the temporary user, until Identity is ready). Minutes and risks are
only shown and saved to ai/output/, because the Meetings and Risks APIs are not ready yet.
"""

import argparse
import json
import sys
from dataclasses import asdict
from datetime import date, datetime
from pathlib import Path

import httpx
from pydantic import BaseModel

from isdlc_ai.agents.base import AgentOutputError, RunInfo, total_tokens
from isdlc_ai.agents.meeting_agent import MeetingAgent, MeetingInput, MinutesResult, OpenIssuesResult, Suggestion
from isdlc_ai.agents.risk_agent import RiskAgent, RiskInput, RiskResult
from isdlc_ai.config import AI_ROOT, Settings, load_settings
from isdlc_ai.isdlc_api import IsdlcApiClient, IsdlcApiError
from isdlc_ai.llm import GeminiClient, LlmError
from isdlc_ai.schemas import MeetingType, OpenIssueType

TYPE_LABELS_TR = {
    OpenIssueType.ACTION: "Aksiyon",
    OpenIssueType.DECISION: "Karar",
    OpenIssueType.QUESTION: "Soru",
    OpenIssueType.DEPENDENCY: "Bağımlılık",
    OpenIssueType.ASSUMPTION: "Varsayım",
    OpenIssueType.CHANGE_REQUEST: "Değişiklik Talebi",
}

MEETING_TYPE_LABELS_TR = {
    MeetingType.REQUEST_INTERVIEW: "Talep Görüşmesi",
    MeetingType.ANALYSIS_SESSION: "Analiz Oturumu",
    MeetingType.KICK_OFF: "Proje Başlangıç",
    MeetingType.REVIEW: "Gözden Geçirme",
    MeetingType.INTERNAL: "Departman İçi",
    MeetingType.CUSTOMER_VISIT: "Müşteri Ziyareti",
    MeetingType.HANDOVER: "Devir Görüşmesi",
    MeetingType.PROJECT_EVALUATION: "Proje Değerlendirme",
}


class UserError(Exception):
    """A problem the user can fix (missing setting, wrong argument, project not found)."""


def main(argv: list[str] | None = None) -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")

    parser = argparse.ArgumentParser(prog="isdlc_ai")
    commands = parser.add_subparsers(dest="command", required=True)

    meeting = commands.add_parser("meeting", help="Toplantı ajanı: notlardan açık konu önerileri (ve tutanak taslağı)")
    meeting.add_argument("--notes", required=True, type=Path, help="Toplantı notlarının bulunduğu metin dosyası")
    meeting.add_argument("--meeting-date", required=True, type=date.fromisoformat, help="YYYY-MM-DD")
    add_project_arguments(meeting)
    meeting.add_argument("--minutes", action="store_true", help="Tutanak taslağını da hazırlar (ikinci görev)")
    meeting.add_argument("--apply", action="store_true", help="Önerileri tek tek onaylatıp açık konu olarak kaydeder")
    meeting.add_argument("--default-owner", help="Sorumlusu belirtilmemiş öneriler için önerilecek ad")

    risks = commands.add_parser("risks", help="Risk ajanı: riskleri bulur, puanlar, önleyici aksiyon önerir")
    source = risks.add_mutually_exclusive_group(required=True)
    source.add_argument("--notes", type=Path, help="Risklerin aranacağı metin dosyası (üç görev de çalışır)")
    source.add_argument("--risk-list", type=Path, help="Her satırda bir risk; yalnızca puanlama ve aksiyon çalışır")
    add_project_arguments(risks)
    risks.add_argument("--source-label", default="Toplantı notları", help="Metnin ne olduğu, ör. \"Analiz dokümanı\"")

    commands.add_parser("models", help="API anahtarının kullanabildiği Gemini modellerini listeler")

    args = parser.parse_args(argv)
    try:
        if args.command == "models":
            return run_models()
        if args.command == "risks":
            return run_risks(args)
        return run_meeting(args)
    except UserError as error:
        print(error)
        return 2
    except LlmError as error:
        print(f"\nModel servisi hata verdi: {error}")
        print(llm_error_hint(error.code))
        return 1
    except AgentOutputError as error:
        print(f"\nModel iki denemede de geçerli bir cevap vermedi: {error}")
        return 1


def add_project_arguments(parser: argparse.ArgumentParser) -> None:
    parser.add_argument("--project-id", type=int, help="Proje bilgisi ISDLC API'sinden okunur")
    parser.add_argument("--project-code", help="API çalışmıyorsa proje kodu (yalnızca öneri)")


def llm_error_hint(code: int | None) -> str:
    if code == 404:
        return ("Model adı yanlış ya da model kapatılmış. ai/.env içindeki GEMINI_MODEL değerini değiştirin; "
                "kullanılabilir modeller için: python -m isdlc_ai models")
    if code in (401, 403):
        return "API anahtarı geçersiz ya da bu modele yetkisi yok. ai/.env içindeki GEMINI_API_KEY değerini kontrol edin."
    if code == 429:
        return "Kota veya istek sınırı doldu. Biraz bekleyip tekrar deneyin ya da başka bir model seçin."
    if code in (500, 503, 504, None):
        return "Model servisi şu an yoğun ya da ulaşılamıyor. Birkaç dakika sonra tekrar deneyin ya da başka bir model seçin."
    return "Hata ayrıntısı yukarıda."


def gemini(settings: Settings) -> GeminiClient:
    if not settings.gemini_api_key or not settings.gemini_model:
        raise UserError("GEMINI_API_KEY ve GEMINI_MODEL ai/.env içinde tanımlı olmalı (bkz. ai/.env.example).")
    return GeminiClient(settings.gemini_api_key, settings.gemini_model)


def resolve_project(api: IsdlcApiClient, args: argparse.Namespace) -> tuple[str, str | None, str | None]:
    """Returns (project code, current step, status). Status is None when only --project-code was given."""
    if not args.project_id:
        if not args.project_code:
            raise UserError("--project-id ya da --project-code verilmeli.")
        return args.project_code, None, None
    try:
        process = api.get_project_process(args.project_id)
    except (IsdlcApiError, httpx.HTTPError) as error:
        raise UserError(f"Proje bilgisi okunamadı: {error}") from error
    step = process.get("currentStep")
    current_step = f"{step['code']} {step['text']}" if step else None
    return process["projectCode"], current_step, process["status"]


# ---------- meeting ----------

def run_models() -> int:
    settings = load_settings()
    if not settings.gemini_api_key:
        raise UserError("GEMINI_API_KEY ai/.env içinde tanımlı olmalı.")
    client = GeminiClient(settings.gemini_api_key, settings.gemini_model or "")
    for name in client.list_text_models():
        marker = "  <- şu an seçili" if name == settings.gemini_model else ""
        print(f"{name}{marker}")
    return 0


def run_meeting(args: argparse.Namespace) -> int:
    settings = load_settings()
    llm = gemini(settings)
    if args.apply and not args.project_id:
        raise UserError("--apply için --project-id gerekli.")

    with IsdlcApiClient(settings.isdlc_api_url) as api:
        project_code, current_step, status = resolve_project(api, args)
        if args.apply and status != "Active":
            raise UserError(f"{project_code} aktif değil ({status}); kayıt eklenemez.")

        meeting = MeetingInput(
            project_code=project_code,
            meeting_date=args.meeting_date,
            notes=args.notes.read_text(encoding="utf-8"),
            current_step=current_step,
        )
        agent = MeetingAgent(llm)

        # Task 1: open issues
        result = agent.suggest_open_issues(meeting)
        print_open_issues(result)
        saved: dict = {"openIssues": asdict(result)}

        # Task 2: minutes (same notes, different prompt)
        if args.minutes:
            minutes = agent.draft_minutes(meeting)
            print_minutes(minutes, result)
            saved["minutes"] = asdict(minutes)
            if minutes.minutes:
                saved["createMeetingDtoPreview"] = minutes.minutes.to_create_dto(
                    result.of_type(OpenIssueType.DECISION), result.of_type(OpenIssueType.ACTION)
                )

        print(f"\nSonuç kaydedildi: {save_result('meeting', project_code, saved)}")

        if args.apply:
            review_and_apply(api, args.project_id, result.suggestions, args.default_owner)
    return 0


def print_open_issues(result: OpenIssuesResult) -> None:
    print(f"\nÖzet: {result.summary}\n")
    for number, suggestion in enumerate(result.suggestions, start=1):
        print_suggestion(number, suggestion)
    print_invalid(result.invalid)
    print_runs([result.run])


def print_minutes(result: MinutesResult, open_issues: OpenIssuesResult) -> None:
    print("\n=== Tutanak taslağı ===")
    if result.minutes is None:
        print(f"Model geçerli bir tutanak taslağı vermedi: {result.invalid_reason}")
        print_runs([result.run])
        return
    minutes = result.minutes
    print(f"Tür: {MEETING_TYPE_LABELS_TR[minutes.type]}   ({minutes.type_reason or '-'})")
    print(f"Tarih: {minutes.date}")
    print(f"Katılımcılar: {minutes.participants}")
    print(f"Gündem:\n{minutes.agenda}")
    decisions = open_issues.of_type(OpenIssueType.DECISION)
    actions = open_issues.of_type(OpenIssueType.ACTION)
    print(f"Kararlar: {len(decisions)}, aksiyonlar: {len(actions)} (açık konu önerilerinden alınır)")
    for warning in result.warnings:
        print(f"! {warning}")
    print("Meetings API'si hazır olunca tutanak bu bilgilerle kaydedilecek; şimdilik yalnızca çıktı dosyasında.")
    print_runs([result.run])


def print_suggestion(number: int, suggestion: Suggestion) -> None:
    item = suggestion.item
    print(f"[{number}] {TYPE_LABELS_TR[item.type]}: {item.title}")
    if item.description:
        print(f"    Açıklama: {item.description}")
    print(f"    Sorumlu: {item.owner_name or '-'}   Termin: {item.due_date or '-'}")
    print(f"    Dayanak: \"{item.evidence}\"")
    for warning in suggestion.warnings:
        print(f"    ! {warning}")
    print()


def review_and_apply(api: IsdlcApiClient, project_id: int, suggestions: list[Suggestion], default_owner: str | None) -> None:
    print("\nÖnerileri inceleyin. e = kaydet, h = atla, q = çık\n")
    created = 0
    for number, suggestion in enumerate(suggestions, start=1):
        print_suggestion(number, suggestion)
        answer = input("Kaydedilsin mi? [e/h/q] ").strip().lower()
        if answer == "q":
            break
        if answer != "e":
            continue

        owner = suggestion.item.owner_name
        if not owner:
            hint = f" [{default_owner}]" if default_owner else ""
            owner = input(f"Sorumlu{hint}: ").strip() or default_owner
            if not owner:
                print("Sorumlu girilmedi, öneri atlandı.\n")
                continue

        try:
            issue = api.create_open_issue(project_id, suggestion.item.to_create_dto(owner_name=owner))
            # Same rule as meeting minutes: a decision is recorded as already closed.
            if suggestion.item.type == OpenIssueType.DECISION:
                api.update_open_issue_status(project_id, issue["id"], "Closed")
        except IsdlcApiError as error:
            print(f"Kaydedilemedi: {error.detail}\n")
            continue
        created += 1
        print(f"Kaydedildi: {issue['code']}\n")

    print(f"{created} açık konu oluşturuldu.")


# ---------- risks ----------

def run_risks(args: argparse.Namespace) -> int:
    settings = load_settings()
    agent = RiskAgent(gemini(settings))

    with IsdlcApiClient(settings.isdlc_api_url) as api:
        project_code, current_step, _ = resolve_project(api, args)

    if args.notes:
        # identify -> assess -> mitigate
        text = args.notes.read_text(encoding="utf-8")
        result = agent.run(RiskInput(project_code, text, args.source_label, current_step))
    else:
        # Risks a person wrote: skip identify, run assess -> mitigate
        titles = args.risk_list.read_text(encoding="utf-8").splitlines()
        result = RiskAgent.from_titles(project_code, titles, current_step)
        if not result.items:
            raise UserError("Risk listesi boş.")
        agent.assess(result)
        agent.mitigate(result)

    print_risks(result)
    print(f"\nSonuç kaydedildi: {save_result('risks', project_code, risk_result_to_dict(result))}")
    return 0


def print_risks(result: RiskResult) -> None:
    if not result.items:
        print("\nMetinde risk bulunamadı.")
    ordered = sorted(result.items, key=lambda item: item.risk.score or 0, reverse=True)
    for number, item in enumerate(ordered, start=1):
        risk = item.risk
        score = f"Skor {risk.score} (olasılık {risk.probability} × etki {risk.impact})" if risk.score else "Puanlanmadı"
        print(f"\n[{number}] {risk.title}")
        print(f"    {score}")
        if risk.description:
            print(f"    Açıklama: {risk.description}")
        if risk.assessment_reason:
            print(f"    Gerekçe: {risk.assessment_reason}")
        if risk.mitigation:
            print("    Önleyici aksiyon:")
            for line in risk.mitigation.splitlines():
                print(f"      {line}")
        if risk.evidence:
            print(f"    Dayanak: \"{risk.evidence}\"")
        for warning in item.warnings:
            print(f"    ! {warning}")
    print()
    print_invalid(result.invalid)
    print_runs(result.runs)
    print("Risks API'si hazır olunca onaylanan riskler kaydedilecek; şimdilik yalnızca çıktı dosyasında.")


def risk_result_to_dict(result: RiskResult) -> dict:
    data = asdict(result)
    data.pop("source_text")  # already in the input file
    return data


# ---------- shared output ----------

def print_invalid(invalid) -> None:
    if invalid:
        print(f"Doğrulamadan geçemeyen {len(invalid)} öneri atlandı:")
        for item in invalid:
            print(f"  - {item.raw.get('title')!r}: {item.reason}")


def print_runs(runs: list[RunInfo]) -> None:
    for run in runs:
        tokens = f", token: {run.input_tokens}/{run.output_tokens}" if run.input_tokens else ""
        print(f"Görev: {run.task}, model: {run.model_name}, prompt: {run.prompt_version}, "
              f"gizlenen veri: {run.masked_count}{tokens}")
    if len(runs) > 1:
        input_tokens, output_tokens = total_tokens(runs)
        if input_tokens:
            print(f"Toplam token: {input_tokens}/{output_tokens} ({len(runs)} model çağrısı)")


def save_result(kind: str, project_code: str, data: dict) -> Path:
    folder = AI_ROOT / "output"
    folder.mkdir(exist_ok=True)
    path = folder / f"{kind}-{project_code}-{datetime.now():%Y%m%d-%H%M%S}.json"
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2, default=_json_default), encoding="utf-8")
    return path


def _json_default(value):
    # asdict() leaves pydantic models as they are; turn them into plain JSON here.
    if isinstance(value, BaseModel):
        return value.model_dump(mode="json")
    return str(value)


if __name__ == "__main__":
    raise SystemExit(main())
