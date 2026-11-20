import json
from datetime import date

from isdlc_ai.agents.request_agent import RequestAgent
from isdlc_ai.llm import FakeLlmClient

EMAIL = (
    "Kimden: Ayşe Kaya <ayse.kaya@ornek.com>\n"
    "Tarih: 18 Kasım 2026\n"
    "Konu: Depo stok takip ekranı\n\n"
    "Merhaba, depodaki ürünlerin stok durumunu anlık izleyebileceğimiz bir ekran istiyoruz. "
    "Yıl sonuna kadar kullanmaya başlamak istiyoruz. Telefon: 0532 111 22 33\n"
    "Ayşe Kaya, Örnek Lojistik A.Ş."
)
TODAY = date(2026, 11, 20)


def answer(**fields):
    base = {
        "name": "Depo stok takip ekranı",
        "description": "Depodaki ürünlerin stok durumunun anlık izlenmesi isteniyor.",
        "customer_name": "Örnek Lojistik A.Ş.",
        "requester_name": "Ayşe Kaya",
        "requester_contact": "[EPOSTA_1]",
        "request_type": "NewProject",
        "request_date": "2026-11-18",
        "due_date": "2026-12-31",
        "notes": None,
        "warnings": [],
    }
    return json.dumps(base | fields, ensure_ascii=False)


def draft(**fields):
    return RequestAgent(FakeLlmClient([answer(**fields)])).draft_from_email(EMAIL, TODAY).draft


def test_draftFromEmail_FillsTheFormAndRestoresTheMaskedContact():
    llm = FakeLlmClient([answer()])

    result = RequestAgent(llm).draft_from_email(EMAIL, TODAY)

    d = result.draft
    assert d.name == "Depo stok takip ekranı"
    assert d.customer_name == "Örnek Lojistik A.Ş."
    assert d.requester_contact == "ayse.kaya@ornek.com"
    assert d.request_type == "NewProject"
    assert d.request_date == date(2026, 11, 18)
    assert d.due_date == date(2026, 12, 31)
    assert d.warnings == []
    assert result.run.prompt_version == "request/from_email.v1"
    assert result.run.masked_count == 2  # e-mail and phone


def test_draftFromEmail_SendsNoPersonalDataToTheModel():
    llm = FakeLlmClient([answer()])

    RequestAgent(llm).draft_from_email(EMAIL, TODAY)

    _, user = llm.calls[0]
    assert "ayse.kaya@ornek.com" not in user
    assert "0532 111 22 33" not in user
    assert "Bugünün tarihi: 2026-11-20" in user


def test_draftFromEmail_UnknownTypeIsDroppedWithAWarning():
    d = draft(request_type="Consulting")

    assert d.request_type is None
    assert "Talep türü anlaşılamadı; elle seçin." in d.warnings


def test_draftFromEmail_MadeUpPlaceholderIsDropped():
    d = draft(requester_contact="[TELEFON_7]")

    assert d.requester_contact is None
    assert "İletişim bilgisi e-postada bulunamadı; elle girin." in d.warnings


def test_draftFromEmail_DueDateBeforeTheRequestIsDropped():
    d = draft(due_date="2026-01-31")

    assert d.due_date is None
    assert any("bitiş tarihi" in w for w in d.warnings)


def test_draftFromEmail_BadDateAndTooLongNameAreDroppedTheRestIsKept():
    d = draft(request_date="18.11.2026", name="x" * 201)

    assert d.request_date is None
    assert d.name is None
    assert d.description is not None
    assert "Talep tarihi anlaşılamadı; elle girin." in d.warnings
    assert "Proje adı 200 karakterden uzun olduğu için doldurulmadı." in d.warnings


def test_draftFromEmail_BlankFieldsAreNull():
    d = draft(customer_name="  ", notes="", warnings=["  ", "İmzadan tahmin edildi."])

    assert d.customer_name is None
    assert d.notes is None
    assert d.warnings == ["İmzadan tahmin edildi."]
