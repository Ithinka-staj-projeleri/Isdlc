import json

import pytest
from fastapi.testclient import TestClient

from isdlc_ai.config import Settings
from isdlc_ai.llm import FakeLlmClient, LlmError
from isdlc_ai.server import create_app

KEY = "test-key"
NOTES = "ERP entegrasyonu için test ortamı henüz yok. Müşteri BT erişimi gelecek cuma gönderecek."


class FailingLlm:
    """The model service refuses every call."""

    model_name = "fake"

    def __init__(self, error: LlmError):
        self.error = error

    def generate_json(self, system, user, schema):
        raise self.error


def client(*responses: str, error: LlmError | None = None) -> TestClient:
    def make_llm():
        return FailingLlm(error) if error is not None else FakeLlmClient(list(responses))

    settings = Settings(isdlc_api_url="http://localhost", gemini_api_key=None, gemini_model=None, agent_service_key=KEY)
    return TestClient(create_app(settings, llm_factory=make_llm), headers={"X-Agent-Key": KEY})


def identified(*risks):
    return json.dumps({"items": list(risks)}, ensure_ascii=False)


RISK = {
    "title": "ERP test ortamı geç gelirse entegrasyon testleri gecikir.",
    "description": None,
    "category": "Technical",
    "evidence": "ERP entegrasyonu için test ortamı henüz yok.",
}
ASSESSED = json.dumps({"items": [{"risk_no": 1, "probability": 4, "impact": 4, "reason": "Erişim tarihi belirsiz."}]},
                      ensure_ascii=False)
MITIGATED = json.dumps({"items": [{"risk_no": 1, "mitigation": "Müşteri BT'den yazılı termin alınır."}]},
                       ensure_ascii=False)


def test_riskSuggestions_ReturnsScoredRisksInCamelCase():
    response = client(identified(RISK), ASSESSED, MITIGATED).post(
        "/v1/risk/suggestions", json={"projectCode": "ISDLC-2026-014", "sourceText": NOTES, "currentStep": "4.8"})

    assert response.status_code == 200
    body = response.json()
    assert body["invalidCount"] == 0
    assert [run["task"] for run in body["runs"]] == ["risk.identify", "risk.assess", "risk.mitigate"]
    item = body["items"][0]
    assert item["category"] == "Technical"
    assert (item["probability"], item["impact"]) == (4, 4)
    assert item["assessmentReason"] == "Erişim tarihi belirsiz."
    assert item["mitigation"] == "Müşteri BT'den yazılı termin alınır."
    assert item["evidence"] == RISK["evidence"]
    assert item["warnings"] == []


def test_riskSuggestions_NoRisk_ReturnsEmptyListAfterOneCall():
    response = client(identified()).post(
        "/v1/risk/suggestions", json={"projectCode": "ISDLC-2026-014", "sourceText": NOTES})

    assert response.status_code == 200
    assert response.json()["items"] == []
    assert len(response.json()["runs"]) == 1


def test_riskAssessment_ScoresOneTypedRisk():
    response = client(ASSESSED, MITIGATED).post(
        "/v1/risk/assessment",
        json={"projectCode": "ISDLC-2026-014", "title": "ERP test ortamı geç gelebilir", "description": None})

    assert response.status_code == 200
    body = response.json()
    assert (body["probability"], body["impact"]) == (4, 4)
    assert body["mitigation"] == "Müşteri BT'den yazılı termin alınır."
    assert [run["task"] for run in body["runs"]] == ["risk.assess", "risk.mitigate"]


def test_riskAssessment_BlankTitle_IsRejected():
    response = client().post("/v1/risk/assessment", json={"projectCode": "ISDLC-2026-014", "title": ""})

    assert response.status_code == 422


@pytest.mark.parametrize("path, body", [
    ("/v1/risk/suggestions", {"projectCode": "P", "sourceText": NOTES}),
    ("/v1/risk/assessment", {"projectCode": "P", "title": "Risk"}),
])
def test_riskEndpoints_WrongKey_Return401(path, body):
    response = client().post(path, json=body, headers={"X-Agent-Key": "yanlis"})

    assert response.status_code == 401


def test_riskSuggestions_ModelQuotaFull_Returns503WithTurkishMessage():
    response = client(error=LlmError("quota", code=429)).post(
        "/v1/risk/suggestions", json={"projectCode": "P", "sourceText": NOTES})

    assert response.status_code == 503
    assert "kotası doldu" in response.json()["detail"]


def test_riskSuggestions_BrokenModelAnswerTwice_Returns503():
    response = client("bozuk", "yine bozuk").post(
        "/v1/risk/suggestions", json={"projectCode": "P", "sourceText": NOTES})

    assert response.status_code == 503
    assert "geçerli bir cevap" in response.json()["detail"]


# ---------- meeting suggestions: open issues and the minutes draft ----------

MEETING_NOTES = "Katılımcılar: Elif Yılmaz, Can Öztürk. Ekran taslaklarını Can Öztürk cuma gününe kadar hazırlayacak."
OPEN_ISSUES = json.dumps({"summary": "Ekran taslakları konuşuldu.", "items": [{
    "type": "Action", "title": "Ekran taslakları", "description": None, "owner_name": "Can Öztürk",
    "due_date": "2026-11-13", "evidence": "Ekran taslaklarını Can Öztürk cuma gününe kadar hazırlayacak."}]},
    ensure_ascii=False)
MINUTES = json.dumps({"meeting_type": "AnalysisSession", "type_reason": "Ekranlar ayrıntılı konuşulmuş.",
                      "participants": ["Elif Yılmaz", "Can Öztürk"], "agenda": ["Ekran taslakları", "Takvim"]},
                     ensure_ascii=False)
MEETING_BODY = {"projectCode": "ISDLC-2026-014", "meetingDate": "2026-11-10", "notes": MEETING_NOTES}


def test_meetingSuggestions_ReturnsOpenIssuesAndMinutesDraft():
    response = client(OPEN_ISSUES, MINUTES).post("/v1/meeting/suggestions", json=MEETING_BODY)

    assert response.status_code == 200
    body = response.json()
    assert body["items"][0]["title"] == "Ekran taslakları"
    assert body["minutes"] == {
        "type": "AnalysisSession",
        "typeReason": "Ekranlar ayrıntılı konuşulmuş.",
        "participants": "Elif Yılmaz, Can Öztürk",
        "agenda": "- Ekran taslakları\n- Takvim",
        "warnings": [],
    }
    assert body["minutesRun"]["task"] == "meeting.minutes"


def test_meetingSuggestions_MinutesTaskFails_StillReturnsOpenIssues():
    response = client(OPEN_ISSUES, "bozuk", "yine bozuk").post("/v1/meeting/suggestions", json=MEETING_BODY)

    assert response.status_code == 200
    assert len(response.json()["items"]) == 1
    assert response.json()["minutes"] is None


def test_meetingSuggestions_UnknownMeetingType_ReturnsNoMinutes():
    bad = json.dumps({"meeting_type": "Party", "type_reason": "", "participants": ["Elif"], "agenda": ["Konu"]})

    response = client(OPEN_ISSUES, bad).post("/v1/meeting/suggestions", json=MEETING_BODY)

    assert response.status_code == 200
    assert response.json()["minutes"] is None
