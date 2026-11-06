from isdlc_ai.masking import mask


def test_mask_ReplacesEmailPhoneIbanAndId():
    text = (
        "Ali'ye ali.veli@ornek.com ya da 0532 123 45 67 ile ulaşılır. "
        "Sabit hat: +90 212 555 12 34. IBAN TR33 0006 1005 1978 6457 8413 26, TCKN 12345678901."
    )

    masked = mask(text)

    assert "ali.veli@ornek.com" not in masked.text
    assert "0532 123 45 67" not in masked.text
    assert "212 555 12 34" not in masked.text
    assert "TR33" not in masked.text
    assert "12345678901" not in masked.text
    assert "[EPOSTA_1]" in masked.text
    assert "[TELEFON_1]" in masked.text and "[TELEFON_2]" in masked.text
    assert "[IBAN_1]" in masked.text
    assert "[TCKN_1]" in masked.text


def test_mask_LeavesDatesAndAmountsUntouched():
    text = "Termin 2026-11-09, bütçe 150.000 TL, 3 ekran ve 12 rapor."

    masked = mask(text)

    assert masked.text == text
    assert masked.replacements == {}


def test_mask_SameValueGetsSamePlaceholder():
    masked = mask("a@b.com ile yazış, cevap gelmezse yine a@b.com adresine yaz.")

    assert masked.text.count("[EPOSTA_1]") == 2
    assert len(masked.replacements) == 1


def test_unmask_RestoresOriginalValues():
    masked = mask("Bilgi için destek@musteri.com.tr adresine yazılacak.")

    restored = masked.unmask("destek adresi: [EPOSTA_1]")

    assert restored == "destek adresi: destek@musteri.com.tr"
    assert masked.unmask(None) is None
