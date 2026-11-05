using System.ComponentModel.DataAnnotations;

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
}

/// <summary>POST .../process/reject — the Software Department Manager rejects the request on any step.</summary>
public record RejectProjectDto
{
    /// <summary>The current step the user saw on screen. If the project has moved on, the request fails with 409.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "Mevcut adım belirtilmelidir.")]
    public int ExpectedStepId { get; init; }

    /// <summary>Why the request is rejected; saved as the note of the StepRecord. Required (also rejects whitespace).</summary>
    [Required(ErrorMessage = "Gerekçe zorunludur.")]
    [MaxLength(1000, ErrorMessage = "Gerekçe en fazla 1000 karakter olabilir.")]
    public string? Reason { get; init; }
}
