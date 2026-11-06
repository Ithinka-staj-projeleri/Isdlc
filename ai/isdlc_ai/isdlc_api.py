"""Small client for the ISDLC API. The agents read and write only through these calls.

Until Identity is ready the backend runs every request as the temporary user (AGENTS.md section 8),
so no token is sent yet. Later the agent service gets its own service account.
"""

import httpx


class IsdlcApiError(Exception):
    def __init__(self, status_code: int, detail: str):
        super().__init__(f"{status_code}: {detail}")
        self.status_code = status_code
        self.detail = detail


class IsdlcApiClient:
    def __init__(self, base_url: str, timeout: float = 15.0, transport: httpx.BaseTransport | None = None):
        self._http = httpx.Client(base_url=f"{base_url}/api/v1", timeout=timeout, transport=transport)

    def close(self) -> None:
        self._http.close()

    def __enter__(self) -> "IsdlcApiClient":
        return self

    def __exit__(self, *exc) -> None:
        self.close()

    def get_project_process(self, project_id: int) -> dict:
        return self._send("GET", f"/projects/{project_id}/process")

    def create_open_issue(self, project_id: int, body: dict) -> dict:
        return self._send("POST", f"/projects/{project_id}/open-issues", json=body)

    def update_open_issue_status(self, project_id: int, open_issue_id: int, status: str) -> dict:
        return self._send("PUT", f"/projects/{project_id}/open-issues/{open_issue_id}/status", json={"status": status})

    def _send(self, method: str, url: str, json: dict | None = None) -> dict:
        response = self._http.request(method, url, json=json)
        if response.is_error:
            raise IsdlcApiError(response.status_code, _problem_detail(response))
        return response.json()


def _problem_detail(response: httpx.Response) -> str:
    """Reads the Turkish message from a ProblemDetails body, including field validation errors."""
    try:
        body = response.json()
    except ValueError:
        return response.text or response.reason_phrase
    errors = body.get("errors")
    if isinstance(errors, dict) and errors:
        return "; ".join(message for messages in errors.values() for message in messages)
    return body.get("detail") or body.get("title") or response.reason_phrase
