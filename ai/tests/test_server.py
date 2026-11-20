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
                      "participants": [{"name": "Elif Yılmaz", "role": None}, {"name": "Can Öztürk", "role": None}],
                      "agenda": ["Ekran taslakları", "Takvim"], "summary": "Ekran taslakları konuşuldu.",
                      "next_steps": ["Taslaklar müşteriye gönderilecek"]},
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
        "participants": [{"name": "Elif Yılmaz", "role": None}, {"name": "Can Öztürk", "role": None}],
        "agenda": "- Ekran taslakları\n- Takvim",
        "summary": "Ekran taslakları konuşuldu.",
        "nextSteps": "- Taslaklar müşteriye gönderilecek",
        "warnings": [],
    }
    assert body["minutesRun"]["task"] == "meeting.minutes"


def test_meetingSuggestions_MinutesTaskFails_StillReturnsOpenIssues():
    response = client(OPEN_ISSUES, "bozuk", "yine bozuk").post("/v1/meeting/suggestions", json=MEETING_BODY)

    assert response.status_code == 200
    assert len(response.json()["items"]) == 1
    assert response.json()["minutes"] is None


def test_meetingSuggestions_UnknownMeetingType_ReturnsNoMinutes():
    bad = json.dumps({"meeting_type": "Party", "type_reason": "", "participants": [{"name": "Elif"}], "agenda": ["Konu"]})

    response = client(OPEN_ISSUES, bad).post("/v1/meeting/suggestions", json=MEETING_BODY)

    assert response.status_code == 200
    assert response.json()["minutes"] is None


def test_requestDraft_ReturnsTheFormInCamelCase():
    answer = json.dumps({
        "name": "Depo stok takip ekranı",
        "description": "Stok durumu anlık izlenecek.",
        "customer_name": "Örnek Lojistik A.Ş.",
        "requester_name": "Ayşe Kaya",
        "requester_contact": "[EPOSTA_1]",
        "request_type": "NewProject",
        "request_date": None,
        "due_date": "2026-12-31",
        "notes": None,
        "warnings": [],
    }, ensure_ascii=False)

    response = client(answer).post("/v1/request/draft", json={
        "emailText": "Ayşe Kaya (ayse.kaya@ornek.com): Depo stok takip ekranı istiyoruz, yıl sonuna kadar.",
        "today": "2026-11-20",
    })

    assert response.status_code == 200
    body = response.json()
    assert body["requesterContact"] == "ayse.kaya@ornek.com"
    assert body["requestType"] == "NewProject"
    assert body["dueDate"] == "2026-12-31"
    assert body["requestDate"] is None
    assert body["run"]["promptVersion"] == "request/from_email.v1"


def test_requestDraft_WithoutKey_Is401():
    settings = Settings(isdlc_api_url="http://localhost", gemini_api_key=None, gemini_model=None, agent_service_key=KEY)
    plain = TestClient(create_app(settings, llm_factory=lambda: FakeLlmClient([])))

    response = plain.post("/v1/request/draft", json={"emailText": "Talep", "today": "2026-11-20"})

    assert response.status_code == 401


class FakeTranscriber:
    model_name = "fake-whisper"

    def __init__(self, text="Merhaba.\nToplantıya başlayalım.", error=None):
        self.text, self.error, self.received = text, error, None

    def transcribe(self, path):
        from isdlc_ai.transcription import Transcript

        self.received = path.read_bytes()
        if self.error:
            raise self.error
        return Transcript(self.text, 61.4, "tr", self.model_name)


def transcribing(transcriber) -> TestClient:
    settings = Settings(isdlc_api_url="http://localhost", gemini_api_key=None, gemini_model=None, agent_service_key=KEY)
    app = create_app(settings, llm_factory=lambda: FakeLlmClient([]), transcriber=transcriber)
    return TestClient(app, headers={"X-Agent-Key": KEY})


def test_transcribe_ReturnsTheTextAndRemovesTheFile():
    fake = FakeTranscriber()

    response = transcribing(fake).post("/v1/meeting/transcribe", content=b"fake audio bytes")

    assert response.status_code == 200
    assert response.json() == {
        "text": "Merhaba.\nToplantıya başlayalım.",
        "durationSeconds": 61,
        "language": "tr",
        "truncated": False,
        "modelName": "fake-whisper",
    }
    assert fake.received == b"fake audio bytes"


def test_transcribe_LongTextIsCutToTheNotesLimit():
    from isdlc_ai.server import MAX_MEETING_NOTES

    response = transcribing(FakeTranscriber(text="a" * (MAX_MEETING_NOTES + 5))).post(
        "/v1/meeting/transcribe", content=b"x")

    assert len(response.json()["text"]) == MAX_MEETING_NOTES
    assert response.json()["truncated"] is True


def test_transcribe_UnreadableFile_Is400():
    from isdlc_ai.transcription import TranscriptionError

    response = transcribing(FakeTranscriber(error=TranscriptionError("not audio"))).post(
        "/v1/meeting/transcribe", content=b"not audio")

    assert response.status_code == 400
    assert response.json()["detail"] == "Kayıt okunamadı; ses ya da video dosyası seçin."


def test_transcribe_EmptyBody_Is400():
    response = transcribing(FakeTranscriber()).post("/v1/meeting/transcribe", content=b"")

    assert response.status_code == 400


def test_transcribe_NotInstalled_Is503AndHealthSaysSo(monkeypatch):
    monkeypatch.setattr("isdlc_ai.server.transcription_available", lambda: False)
    settings = Settings(isdlc_api_url="http://localhost", gemini_api_key=None, gemini_model=None, agent_service_key=KEY)
    off = TestClient(create_app(settings, llm_factory=lambda: FakeLlmClient([])), headers={"X-Agent-Key": KEY})

    assert off.get("/health").json()["transcriptionAvailable"] is False
    assert off.post("/v1/meeting/transcribe", content=b"x").status_code == 503
