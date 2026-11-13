using System.ComponentModel.DataAnnotations;
using Isdlc.Domain.Projects;

namespace Isdlc.Application.Process.Dtos;

/// <summary>POST .../process/complete — completes the current (non-decision) step.</summary>
public record CompleteStepDto
{
    /// <summary>The current step the user saw on screen. If the project has moved on, the request fails with 409.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "Mevcut adım belirtilmelidir.")]
    public int ExpectedStepId { get; init; }

    [MaxLength(1000, ErrorMessage = "Not en fazla 1000 karakter olabilir.")]
    public string? Note { get; init; }
}

public enum StepAnswer
{
    Yes,
    No,
}

/// <summary>POST .../process/answer — answers the current decision step.</summary>
public record AnswerStepDto
{
    /// <summary>The current step the user saw on screen. If the project has moved on, the request fails with 409.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "Mevcut adım belirtilmelidir.")]
    public int ExpectedStepId { get; init; }

    // Nullable so a missing answer is an error instead of silently becoming "Yes" (the enum's first value).
    [Required(ErrorMessage = "Cevap (Evet/Hayır) belirtilmelidir.")]
    public StepAnswer? Answer { get; init; }

    /// <summary>Only for "No" on a step with return options: which step to go back to.</summary>
    public int? JumpTargetStepId { get; init; }

    [MaxLength(1000, ErrorMessage = "Not en fazla 1000 karakter olabilir.")]
    public string? Note { get; init; }

    /// <summary>
    /// The Talep Ret / Erteleme Formu, required for a "No" that closes a request (yeni proje 4 and 11); recorded as a
    /// rejection. Ignored for every other answer.
    /// </summary>
    public RequestDecisionFormDto? ClosingDecision { get; init; }
}

/// <summary>POST .../process/reject — the Software Department Manager rejects the request on any step.</summary>
public record RejectProjectDto
{
    /// <summary>The current step the user saw on screen. If the project has moved on, the request fails with 409.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "Mevcut adım belirtilmelidir.")]
    public int ExpectedStepId { get; init; }

    /// <summary>The Talep Ret / Erteleme Formu; its explanation is also the note of the StepRecord.</summary>
    [Required(ErrorMessage = "Karar bilgileri girilmelidir.")]
    public RequestDecisionFormDto? Decision { get; init; }
}

/// <summary>
/// POST .../process/postpone — the Software Department Manager postpones a request: it waits on the same step
/// until it is taken back (resume). Only a request (before its flow's project stage) can be postponed.
/// </summary>
public record PostponeProjectDto : IValidatableObject
{
    /// <summary>The current step the user saw on screen. If the project has moved on, the request fails with 409.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "Mevcut adım belirtilmelidir.")]
    public int ExpectedStepId { get; init; }

    [Required(ErrorMessage = "Karar bilgileri girilmelidir.")]
    public RequestDecisionFormDto? Decision { get; init; }

    /// <summary>Yeniden değerlendirme tarihi: when the request is looked at again.</summary>
    [Required(ErrorMessage = "Yeniden değerlendirme tarihi zorunludur.")]
    public DateOnly? ReevaluationDate { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ReevaluationDate is { } reevaluation && Decision?.DecisionDate is { } decided && reevaluation <= decided)
        {
            yield return new ValidationResult(
                "Yeniden değerlendirme tarihi karar tarihinden sonra olmalıdır.", [nameof(ReevaluationDate)]);
        }
    }
}

/// <summary>POST .../process/resume — the Software Department Manager takes a postponed request back; it continues on its step.</summary>
public record ResumeProjectDto
{
    /// <summary>The step the request waits on, as the user saw it. If it has changed, the request fails with 409.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "Mevcut adım belirtilmelidir.")]
    public int ExpectedStepId { get; init; }

    [MaxLength(1000, ErrorMessage = "Not en fazla 1000 karakter olabilir.")]
    public string? Note { get; init; }
}

/// <summary>
/// The Talep Ret / Erteleme Formu (ISDLC-Form-Analiz-007) without the request's own fields and the re-evaluation date.
/// Contract: docs/process-api.md → "Ret / erteleme kararı".
/// </summary>
public record RequestDecisionFormDto : IValidatableObject
{
    [Required(ErrorMessage = "Karar tarihi zorunludur.")]
    public DateOnly? DecisionDate { get; init; }

    /// <summary>Kararı veren (kişi / kurul): free text, e.g. "Yazılım Departman Yöneticisi" or "Proje Kurulu".</summary>
    [Required(ErrorMessage = "Kararı veren kişi ya da kurul zorunludur.")]
    [MaxLength(200, ErrorMessage = "Kararı veren en fazla 200 karakter olabilir.")]
    public string? DecidedBy { get; init; }

    /// <summary>One or more reason categories; a repeated one counts once.</summary>
    [Required(ErrorMessage = "En az bir gerekçe kategorisi seçilmelidir.")]
    [MinLength(1, ErrorMessage = "En az bir gerekçe kategorisi seçilmelidir.")]
    public List<DecisionReason>? Reasons { get; init; }

    /// <summary>What "Diğer" means; required when Reasons contains Other, ignored otherwise.</summary>
    [MaxLength(200, ErrorMessage = "Diğer gerekçe en fazla 200 karakter olabilir.")]
    public string? ReasonOther { get; init; }

    [Required(ErrorMessage = "Gerekçe açıklaması zorunludur.")]
    [MaxLength(2000, ErrorMessage = "Gerekçe açıklaması en fazla 2000 karakter olabilir.")]
    public string? Explanation { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Reasons?.Contains(DecisionReason.Other) == true && string.IsNullOrWhiteSpace(ReasonOther))
        {
            yield return new ValidationResult("Diğer gerekçe yazılmalıdır.", [nameof(ReasonOther)]);
        }
    }
}
