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
