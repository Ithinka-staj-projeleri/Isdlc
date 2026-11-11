"""Masks personal data before text is sent to an LLM, and restores it in the answer.

Covers e-mail, phone, IBAN and Turkish ID numbers. Person names are not masked yet;
that needs a named-entity tool (e.g. Microsoft Presidio) and is a later step.
"""

import re
from dataclasses import dataclass, field

# Order matters: IBAN before phone and ID, so its digits are not split up.
_PATTERNS: list[tuple[str, re.Pattern[str]]] = [
    ("EPOSTA", re.compile(r"[\w.+-]+@[\w-]+(?:\.[\w-]+)+")),
    ("IBAN", re.compile(r"\bTR\d{2}(?:\s?\d{4}){5}\s?\d{2}\b", re.IGNORECASE)),
    ("TELEFON", re.compile(r"(?<!\d)(?:\+90[\s-]?|0)?[2-5]\d{2}[\s-]?\d{3}[\s-]?\d{2}[\s-]?\d{2}(?!\d)")),
    ("TCKN", re.compile(r"(?<!\d)[1-9]\d{10}(?!\d)")),
]


# What mask() writes in place of personal data. One left in an answer after unmask() was made up by the model.
PLACEHOLDER = re.compile(r"\[(?:EPOSTA|IBAN|TELEFON|TCKN)_\d+\]")


@dataclass
class MaskedText:
    text: str
    replacements: dict[str, str] = field(default_factory=dict)  # placeholder -> original

    def unmask(self, value: str | None) -> str | None:
        if value is None:
            return None
        for placeholder, original in self.replacements.items():
            value = value.replace(placeholder, original)
        return value


def mask(text: str) -> MaskedText:
    replacements: dict[str, str] = {}
    seen: dict[str, str] = {}  # original -> placeholder, so the same value gets the same placeholder

    for label, pattern in _PATTERNS:
        counter = sum(1 for p in replacements if p.startswith(f"[{label}_"))

        def replace(match: re.Match[str]) -> str:
            nonlocal counter
            original = match.group(0)
            if original in seen:
                return seen[original]
            counter += 1
            placeholder = f"[{label}_{counter}]"
            seen[original] = placeholder
            replacements[placeholder] = original
            return placeholder

        text = pattern.sub(replace, text)

    return MaskedText(text=text, replacements=replacements)
