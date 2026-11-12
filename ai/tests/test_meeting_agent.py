import json
from datetime import date

import pytest

from isdlc_ai.agents.base import AgentOutputError
from isdlc_ai.agents.meeting_agent import MeetingAgent, MeetingInput
from isdlc_ai.llm import FakeLlmClient
from isdlc_ai.schemas import MeetingType, OpenIssueType

NOTES = (
    "Müşteri BT, ERP test ortamı erişimini gelecek cuma gönderecek. "
    "İş emri durumlarının 6 adım olmasına karar verildi. "
    "Can Öztürk ekran taslaklarını hazırlayacak. "
    "İletişim: bt@musteri.com"
)
MEETING = MeetingInput(project_code="ISDLC-2026-014", meeting_date=date(2026, 11, 4), notes=NOTES)


def answer(items, summary="Özet."):
    return json.dumps({"summary": summary, "items": items}, ensure_ascii=False)


def item(**overrides):
    base = {
        "type": "Action",
        "title": "Ekran taslaklarının hazırlanması",
        "description": None,
        "owner_name": "Can Öztürk",
        "due_date": None,
        "evidence": "Can Öztürk ekran taslaklarını hazırlayacak.",
    }
    return base | overrides


def test_suggestOpenIssues_ReturnsValidSuggestions():
    llm = FakeLlmClient([
        answer([
            item(),
            item(
                type="Dependency",
                title="ERP test ortamı erişimi",
                owner_name="Müşteri BT",
                due_date="2026-11-13",
                evidence="Müşteri BT, ERP test ortamı erişimini gelecek cuma gönderecek.",
            ),
            item(
                type="Decision",
                title="İş emri durumları 6 adım",
                owner_name=None,
                evidence="İş emri durumlarının 6 adım olmasına karar verildi.",
            ),
        ])
    ])

    result = MeetingAgent(llm).suggest_open_issues(MEETING)

    assert [s.item.type for s in result.suggestions] == [
        OpenIssueType.ACTION,
        OpenIssueType.DEPENDENCY,
        OpenIssueType.DECISION,
    ]
    assert result.suggestions[1].item.due_date == date(2026, 11, 13)
    assert result.suggestions[0].warnings == []
    assert result.invalid == []


def test_suggestOpenIssues_SendsMaskedNotesToModel():
    llm = FakeLlmClient([answer([])])

    result = MeetingAgent(llm).suggest_open_issues(MEETING)

    _, user_message = llm.calls[0]
    assert "bt@musteri.com" not in user_message
    assert "[EPOSTA_1]" in user_message
    assert "2026-11-04 (Çarşamba)" in user_message
    assert result.run.masked_count == 1


def test_suggestOpenIssues_RestoresMaskedValuesInSuggestions():
    llm = FakeLlmClient([
        answer([item(type="Question", title="[EPOSTA_1] adresine kim yazacak?", evidence="İletişim: [EPOSTA_1]")])
    ])

    result = MeetingAgent(llm).suggest_open_issues(MEETING)

    assert result.suggestions[0].item.title == "bt@musteri.com adresine kim yazacak?"
    assert result.suggestions[0].item.evidence == "İletişim: bt@musteri.com"


def test_suggestOpenIssues_PutsInvalidItemsAsideAndKeepsTheRest():
    llm = FakeLlmClient([
        answer([
            item(),
            item(type="Risk", title="Geçersiz tür"),
            item(title="x" * 301),
            item(due_date="gelecek hafta"),
        ])
    ])

    result = MeetingAgent(llm).suggest_open_issues(MEETING)

    assert len(result.suggestions) == 1
    assert len(result.invalid) == 3


def test_suggestOpenIssues_WarnsWhenEvidenceIsNotInNotesOrOwnerIsMissing():
    llm = FakeLlmClient([
        answer([item(owner_name=None, evidence="Böyle bir cümle notlarda yok.", due_date="2026-10-01")])
    ])

    result = MeetingAgent(llm).suggest_open_issues(MEETING)

    warnings = result.suggestions[0].warnings
    assert len(warnings) == 3
    assert any("Dayanak" in w for w in warnings)
    assert any("Termin" in w for w in warnings)
    assert any("Sorumlu" in w for w in warnings)


def test_suggestOpenIssues_EvidenceCheckIgnoresCaseAndPunctuation():
    llm = FakeLlmClient([answer([item(evidence="can öztürk ekran taslaklarını hazırlayacak")])])

    result = MeetingAgent(llm).suggest_open_issues(MEETING)

    assert result.suggestions[0].warnings == []


def test_suggestOpenIssues_RetriesOnceWhenJsonIsBroken():
    llm = FakeLlmClient(["bu json değil", answer([item()])])

    result = MeetingAgent(llm).suggest_open_issues(MEETING)

    assert len(llm.calls) == 2
    assert "şemaya uymadı" in llm.calls[1][1]
    assert len(result.suggestions) == 1


def test_suggestOpenIssues_FailsWhenJsonIsBrokenTwice():
    llm = FakeLlmClient(["bozuk", "yine bozuk"])

    with pytest.raises(AgentOutputError):
        MeetingAgent(llm).suggest_open_issues(MEETING)


# ---------- second task: minutes ----------

def minutes_answer(**overrides):
    base = {
        "meeting_type": "AnalysisSession",
        "type_reason": "Gereksinimler ayrıntılı konuşulmuş.",
        "participants": [{"name": "Can Öztürk", "role": None}, {"name": "Müşteri BT", "role": None}],
        "agenda": ["ERP test ortamı", "İş emri durumları"],
        "summary": "ERP test ortamı ve iş emri durumları konuşuldu.",
        "next_steps": ["Ekran taslakları hazırlanacak"],
    }
    return json.dumps(base | overrides, ensure_ascii=False)


def test_draftMinutes_UsesItsOwnPromptOnTheSameNotes():
    llm = FakeLlmClient([answer([]), minutes_answer()])
    agent = MeetingAgent(llm)

    agent.suggest_open_issues(MEETING)
    agent.draft_minutes(MEETING)

    (open_issues_prompt, open_issues_message), (minutes_prompt, minutes_message) = llm.calls
    assert open_issues_prompt != minutes_prompt
    assert "tutanağının bilgilerini" in minutes_prompt
    assert open_issues_message == minutes_message


def test_draftMinutes_ReturnsMinutesWithMeetingDate():
    llm = FakeLlmClient([minutes_answer()])

    result = MeetingAgent(llm).draft_minutes(MEETING)

    assert result.minutes.type == MeetingType.ANALYSIS_SESSION
    assert result.minutes.date == date(2026, 11, 4)
    assert [(p.name, p.role) for p in result.minutes.participants] == [("Can Öztürk", None), ("Müşteri BT", None)]
    assert result.minutes.agenda == "- ERP test ortamı\n- İş emri durumları"
    assert result.minutes.summary == "ERP test ortamı ve iş emri durumları konuşuldu."
    assert result.minutes.next_steps == "- Ekran taslakları hazırlanacak"
    assert result.warnings == []
    assert result.run.prompt_version == "meeting/minutes.v2"


def test_draftMinutes_WarnsAboutParticipantNotInNotes():
    llm = FakeLlmClient([minutes_answer(participants=[{"name": "Can Öztürk"}, {"name": "Ayşe Kaya"}])])

    result = MeetingAgent(llm).draft_minutes(MEETING)

    assert result.warnings == ["Katılımcı notlarda geçmiyor: Ayşe Kaya"]


def test_draftMinutes_KeepsRolesFromTheNotes_WarnsAboutAnInventedRole():
    llm = FakeLlmClient([minutes_answer(participants=[
        {"name": " Müşteri BT ", "role": "  "},
        {"name": "Can Öztürk", "role": "Yazılım Geliştirici"},
    ])])

    result = MeetingAgent(llm).draft_minutes(MEETING)

    assert [(p.name, p.role) for p in result.minutes.participants] == [
        ("Müşteri BT", None), ("Can Öztürk", "Yazılım Geliştirici")]
    assert result.warnings == ["Rol notlarda geçmiyor: Can Öztürk (Yazılım Geliştirici)"]


def test_draftMinutes_NoSummaryOrNextSteps_LeavesThemEmpty():
    llm = FakeLlmClient([minutes_answer(summary="  ", next_steps=[" ", ""])])

    result = MeetingAgent(llm).draft_minutes(MEETING)

    assert result.minutes.summary is None
    assert result.minutes.next_steps is None


def test_draftMinutes_NoParticipants_ReturnsNoMinutes():
    llm = FakeLlmClient([minutes_answer(participants=[{"name": "  "}])])

    result = MeetingAgent(llm).draft_minutes(MEETING)

    assert result.minutes is None
    assert "participants" in result.invalid_reason


def test_draftMinutes_ReturnsNoMinutesWhenTypeIsInvalid():
    llm = FakeLlmClient([minutes_answer(meeting_type="Workshop")])

    result = MeetingAgent(llm).draft_minutes(MEETING)

    assert result.minutes is None
    assert "type" in result.invalid_reason


def test_toCreateDto_CombinesMinutesWithDecisionsAndActions():
    llm = FakeLlmClient([
        answer([
            item(),
            item(type="Decision", title="İş emri durumları 6 adım", owner_name=None,
                 evidence="İş emri durumlarının 6 adım olmasına karar verildi."),
        ]),
        minutes_answer(),
    ])
    agent = MeetingAgent(llm)
    open_issues = agent.suggest_open_issues(MEETING)
    minutes = agent.draft_minutes(MEETING)

    dto = minutes.minutes.to_create_dto(
        open_issues.of_type(OpenIssueType.DECISION), open_issues.of_type(OpenIssueType.ACTION)
    )

    assert dto["type"] == "AnalysisSession"
    assert dto["date"] == "2026-11-04"
    assert dto["participants"] == [{"name": "Can Öztürk", "role": None}, {"name": "Müşteri BT", "role": None}]
    assert dto["recordedByUserId"] is None
    assert dto["nextSteps"] == "- Ekran taslakları hazırlanacak"
    assert dto["decisions"] == ["İş emri durumları 6 adım"]
    assert dto["actions"] == [
        {"title": "Ekran taslaklarının hazırlanması", "ownerUserId": None, "ownerName": "Can Öztürk", "dueDate": None}
    ]
