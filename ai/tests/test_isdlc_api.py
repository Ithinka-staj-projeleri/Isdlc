from datetime import date

import httpx
import pytest

from isdlc_ai.isdlc_api import IsdlcApiClient, IsdlcApiError
from isdlc_ai.schemas import OpenIssueSuggestion, OpenIssueType


def client_with(handler):
    return IsdlcApiClient("http://isdlc.test", transport=httpx.MockTransport(handler))


def test_createOpenIssue_SendsCreateOpenIssueDto():
    sent = {}

    def handler(request: httpx.Request) -> httpx.Response:
        sent["url"] = str(request.url)
        sent["body"] = request.read().decode()
        return httpx.Response(201, json={"id": 7, "code": "AK-004"})

    suggestion = OpenIssueSuggestion(
        type=OpenIssueType.DEPENDENCY,
        title="ERP test ortamı erişimi",
        owner_name="Müşteri BT",
        due_date=date(2026, 11, 13),
        evidence="...",
    )

    with client_with(handler) as api:
        issue = api.create_open_issue(3, suggestion.to_create_dto())

    assert issue["code"] == "AK-004"
    assert sent["url"] == "http://isdlc.test/api/v1/projects/3/open-issues"
    assert '"type":"Dependency"' in sent["body"]
    assert '"ownerUserId":null' in sent["body"]
    assert '"dueDate":"2026-11-13"' in sent["body"]
    assert "evidence" not in sent["body"]


def test_send_RaisesWithProblemDetailsMessage():
    def handler(request: httpx.Request) -> httpx.Response:
        return httpx.Response(409, json={"title": "Conflict", "status": 409, "detail": "Proje aktif değil; kayıt eklenemez veya değiştirilemez."})

    with client_with(handler) as api, pytest.raises(IsdlcApiError) as error:
        api.create_open_issue(3, {})

    assert error.value.status_code == 409
    assert error.value.detail.startswith("Proje aktif değil")


def test_send_JoinsFieldValidationErrors():
    def handler(request: httpx.Request) -> httpx.Response:
        return httpx.Response(400, json={"title": "Bad Request", "errors": {"title": ["Konu zorunludur."]}})

    with client_with(handler) as api, pytest.raises(IsdlcApiError) as error:
        api.create_open_issue(3, {})

    assert error.value.detail == "Konu zorunludur."
