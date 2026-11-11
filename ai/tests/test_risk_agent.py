import json

from isdlc_ai.agents.risk_agent import RiskAgent, RiskInput
from isdlc_ai.llm import FakeLlmClient
from isdlc_ai.schemas import RiskCategory

NOTES = (
    "ERP entegrasyonu için test ortamı henüz yok. Müşteri BT erişimi gelecek cuma gönderecek. "
    "Teknisyenlerin hepsinin Android kullandığı varsayıldı. "
    "Sorular için: bt@musteri.com"
)
SOURCE = RiskInput(project_code="ISDLC-2026-014", source_text=NOTES)


def identified(*risks):
    return json.dumps({"items": list(risks)}, ensure_ascii=False)


def risk(title="ERP test ortamı geç gelirse entegrasyon testleri gecikir.",
         evidence="ERP entegrasyonu için test ortamı henüz yok.", category="Technical"):
    return {"title": title, "description": None, "category": category, "evidence": evidence}


def assessed(*items):
    return json.dumps({"items": [
        {"risk_no": no, "probability": p, "impact": i, "reason": "Gerekçe."} for no, p, i in items
    ]})


def mitigated(*items):
    return json.dumps({"items": [{"risk_no": no, "mitigation": text} for no, text in items]}, ensure_ascii=False)


def test_run_ChainsThreeTasksWithTheirOwnPrompts():
    llm = FakeLlmClient([
        identified(risk()),
        assessed((1, 4, 5)),
        mitigated((1, "- Müşteri BT'den yazılı termin alınır")),
    ])

    result = RiskAgent(llm).run(SOURCE)

    assert [run.prompt_version for run in result.runs] == ["risk/identify.v2", "risk/assess.v1", "risk/mitigate.v2"]
    assert len({system for system, _ in llm.calls}) == 3
    item = result.items[0]
    assert (item.risk.probability, item.risk.impact, item.risk.score) == (4, 5, 20)
    assert item.risk.mitigation == "- Müşteri BT'den yazılı termin alınır"
    assert item.warnings == []


def test_run_GivesEarlierResultsToLaterTasks():
    llm = FakeLlmClient([identified(risk()), assessed((1, 4, 5)), mitigated((1, "- Aksiyon"))])

    RiskAgent(llm).run(SOURCE)

    _, assess_message = llm.calls[1]
    _, mitigate_message = llm.calls[2]
    assert "1. ERP test ortamı geç gelirse" in assess_message
    assert "Skor" not in assess_message
    assert "Olasılık: 4, Etki: 5, Skor: 20" in mitigate_message


def test_run_MasksPersonalDataInEveryTask():
    llm = FakeLlmClient([identified(risk()), assessed((1, 3, 3)), mitigated((1, "- Aksiyon"))])

    RiskAgent(llm).run(SOURCE)

    assert all("bt@musteri.com" not in message for _, message in llm.calls)


def test_run_StopsAfterIdentifyWhenNoRiskFound():
    llm = FakeLlmClient([identified()])

    result = RiskAgent(llm).run(SOURCE)

    assert result.items == []
    assert len(llm.calls) == 1


def test_assess_LeavesRiskUnscoredWhenScoreIsOutOfRange():
    llm = FakeLlmClient([identified(risk()), assessed((1, 7, 2)), mitigated((1, "- Aksiyon"))])

    result = RiskAgent(llm).run(SOURCE)

    assert result.items[0].risk.score is None
    assert any("geçersiz puan" in w for w in result.items[0].warnings)


def test_assess_WarnsWhenARiskIsSkipped():
    llm = FakeLlmClient([
        identified(risk(), risk(title="Android varsayımı yanlışsa iOS ihtiyacı çıkar.",
                                evidence="Teknisyenlerin hepsinin Android kullandığı varsayıldı.")),
        assessed((1, 3, 4)),
        mitigated((1, "- Aksiyon"), (2, "- Aksiyon")),
    ])

    result = RiskAgent(llm).run(SOURCE)

    assert result.items[0].risk.score == 12
    assert "Model bu riski puanlamadı." in result.items[1].warnings


def test_identify_WarnsWhenEvidenceIsNotInText():
    llm = FakeLlmClient([identified(risk(evidence="Böyle bir cümle yok."))])

    result = RiskAgent(llm).identify(SOURCE)

    assert any("Dayanak" in w for w in result.items[0].warnings)


def test_fromTitles_RunsOnlyAssessAndMitigate():
    llm = FakeLlmClient([assessed((1, 2, 3), (2, 4, 4)), mitigated((1, "- A"), (2, "- B"))])
    agent = RiskAgent(llm)

    result = RiskAgent.from_titles("ISDLC-2026-014", ["Anahtar kullanıcılar UAT'ye katılamaz", "", "Lisans gecikir"])
    agent.assess(result)
    agent.mitigate(result)

    assert len(llm.calls) == 2
    assert [item.risk.score for item in result.items] == [6, 16]
    assert [run.task for run in result.runs] == ["risk.assess", "risk.mitigate"]


def test_identify_KeepsTheCategory():
    llm = FakeLlmClient([identified(risk(category="Supplier"))])

    result = RiskAgent(llm).identify(SOURCE)

    assert result.items[0].risk.category == RiskCategory.SUPPLIER
    assert result.items[0].warnings == []


def test_identify_UnknownOrMissingCategory_KeepsTheRiskWithoutCategory():
    llm = FakeLlmClient([identified(risk(category="Hava durumu"), risk(title="İkinci risk", category=" "))])

    result = RiskAgent(llm).identify(SOURCE)

    assert [item.risk.category for item in result.items] == [None, None]
    assert any("kategori" in w for w in result.items[0].warnings)
    assert not any("kategori" in w for w in result.items[1].warnings)
    assert result.invalid == []


def test_fromRisk_SendsTitleAndDescriptionToAssess():
    llm = FakeLlmClient([assessed((1, 3, 4)), mitigated((1, "- Lisans siparişi erkene alınır"))])
    agent = RiskAgent(llm)

    result = RiskAgent.from_risk("ISDLC-2026-014", "  Lisans gecikir  ", "Tedarikçi stokta yok dedi.", "4.8 Riskler")
    agent.mitigate(agent.assess(result))

    _, assess_message = llm.calls[0]
    assert "1. Lisans gecikir" in assess_message
    assert "Açıklama: Tedarikçi stokta yok dedi." in assess_message
    assert "4.8 Riskler" in assess_message
    assert (result.items[0].risk.score, result.items[0].risk.mitigation) == (12, "- Lisans siparişi erkene alınır")


def test_mitigate_PlaceholderTheModelMadeUp_IsFlagged():
    llm = FakeLlmClient([identified(risk()), assessed((1, 3, 3)),
                         mitigated((1, "- [TELEFON_1] aranarak müşteri BT hatırlatılır"))])

    result = RiskAgent(llm).run(SOURCE)

    assert any("yer tutucu" in w for w in result.items[0].warnings)


def test_mitigate_RealMaskedValue_IsRestoredAndNotFlagged():
    # The notes have bt@musteri.com; the model may repeat its placeholder, which is put back.
    llm = FakeLlmClient([identified(risk()), assessed((1, 3, 3)),
                         mitigated((1, "- [EPOSTA_1] adresine yazılı termin sorulur"))])

    result = RiskAgent(llm).run(SOURCE)

    assert result.items[0].risk.mitigation == "- bt@musteri.com adresine yazılı termin sorulur"
    assert not any("yer tutucu" in w for w in result.items[0].warnings)
