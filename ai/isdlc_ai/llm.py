"""LLM access behind one small interface, so the provider can change without touching the agents."""

import time
from dataclasses import dataclass, field
from typing import Protocol

from pydantic import BaseModel

# Errors where another model may still answer: overloaded, quota, server error, timeout (None).
FALLBACK_CODES = {429, 500, 503, 504, None}


@dataclass
class LlmResult:
    text: str
    input_tokens: int | None = None
    output_tokens: int | None = None


@dataclass
class FailedAttempt:
    model: str
    code: int | None
    message: str


class LlmError(Exception):
    """The model service refused or failed the request (overloaded, quota, bad key, wrong model...)."""

    def __init__(self, message: str, code: int | None = None, attempts: list[FailedAttempt] | None = None):
        super().__init__(message)
        self.code = code
        self.attempts = attempts or []


class LlmClient(Protocol):
    model_name: str

    def generate_json(self, system: str, user: str, schema: type[BaseModel]) -> LlmResult:
        """Returns JSON text that should match `schema`. The caller still validates it."""
        ...


class GeminiClient:
    """Gemini with a fallback chain: if a model is overloaded or out of quota, the next one is tried.

    `model_name` is the model that answered the last request.
    """

    def __init__(
        self,
        api_key: str,
        model_name: str,
        fallback_models: list[str] | None = None,
        temperature: float = 0.2,
        client=None,
    ):
        if client is None:
            # Imported here so tests and the fake client work without the SDK installed.
            from google import genai
            from google.genai import types

            client = genai.Client(
                api_key=api_key,
                http_options=types.HttpOptions(
                    timeout=60_000,  # ms
                    # The SDK's own retries wait a long time on 503; keep them short and move to the next model instead.
                    retry_options=types.HttpRetryOptions(attempts=2, initial_delay=1, max_delay=4),
                ),
            )
        self._client = client
        self._models = [model_name] + [m for m in (fallback_models or []) if m and m != model_name]
        self.model_name = model_name
        self._temperature = temperature

    @property
    def models(self) -> list[str]:
        return list(self._models)

    def generate_json(self, system: str, user: str, schema: type[BaseModel]) -> LlmResult:
        from google.genai import types

        config = types.GenerateContentConfig(
            system_instruction=system,
            response_mime_type="application/json",
            response_schema=schema,
            temperature=self._temperature,
        )
        failed: list[FailedAttempt] = []
        for model in self._models:
            try:
                response = self._client.models.generate_content(model=model, contents=user, config=config)
            except Exception as error:  # noqa: BLE001 — converted below; unknown errors are re-raised
                attempt = _to_attempt(model, error)
                failed.append(attempt)
                if attempt.code in FALLBACK_CODES:
                    continue
                raise LlmError(attempt.message, attempt.code, failed) from error

            self.model_name = model
            usage = response.usage_metadata
            return LlmResult(
                text=response.text or "",
                input_tokens=usage.prompt_token_count if usage else None,
                output_tokens=usage.candidates_token_count if usage else None,
            )

        last = failed[-1]
        raise LlmError(f"Denenen {len(failed)} modelin hiçbiri cevap vermedi.", last.code, failed)

    def ping(self, model: str) -> float:
        """Sends a tiny request to one model; returns the seconds it took or raises LlmError."""
        started = time.perf_counter()
        try:
            self._client.models.generate_content(model=model, contents="Yalnızca 'tamam' yaz.")
        except Exception as error:  # noqa: BLE001
            attempt = _to_attempt(model, error)
            raise LlmError(attempt.message, attempt.code, [attempt]) from error
        return time.perf_counter() - started

    def list_text_models(self) -> list[str]:
        """Model names this key can use for text generation."""
        try:
            models = list(self._client.models.list())
        except Exception as error:  # noqa: BLE001
            attempt = _to_attempt("-", error)
            raise LlmError(attempt.message, attempt.code, [attempt]) from error
        return sorted(
            model.name.removeprefix("models/")
            for model in models
            if "generateContent" in (model.supported_actions or [])
        )


def _to_attempt(model: str, error: Exception) -> FailedAttempt:
    """Turns SDK and network errors into one shape. Anything else is a bug and is re-raised."""
    import httpx
    from google.genai import errors

    if isinstance(error, errors.APIError):
        return FailedAttempt(model, error.code, f"Gemini {error.code}: {error.message}")
    if isinstance(error, httpx.HTTPError):
        return FailedAttempt(model, None, f"Bağlantı hatası: {error}")
    raise error


@dataclass
class FakeLlmClient:
    """Returns prepared answers in order. Used by tests and --demo; records what was sent."""

    responses: list[str]
    model_name: str = "fake"
    calls: list[tuple[str, str]] = field(default_factory=list)

    def generate_json(self, system: str, user: str, schema: type[BaseModel]) -> LlmResult:
        self.calls.append((system, user))
        if not self.responses:
            raise AssertionError("FakeLlmClient has no more prepared responses.")
        return LlmResult(text=self.responses.pop(0))
