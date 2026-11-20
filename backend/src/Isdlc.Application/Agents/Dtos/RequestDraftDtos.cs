using System.ComponentModel.DataAnnotations;
using Isdlc.Domain.Projects;

namespace Isdlc.Application.Agents.Dtos;

/// <summary>POST /api/v1/agents/request-draft — the e-mail a request came with.</summary>
public record DraftRequestFromEmailDto
{
    [Required(ErrorMessage = "E-posta metni zorunludur.")]
    [MaxLength(RequestDraftLimits.MaxEmail, ErrorMessage = "E-posta en fazla 20000 karakter olabilir.")]
    public string? EmailText { get; init; }
}

/// <summary>
/// The new request form filled from an e-mail; nothing is saved. A field is null when the e-mail does not say it or
/// when the suggestion would not pass the form's own rules. Channel is not here: the form sets it to E-posta.
/// Contract: docs/projects-api.md → "Yapay zekâ: e-postadan talep taslağı".
/// </summary>
public record RequestDraftDto(
    string? Name,
    string? Description,
    string? CustomerName,
    string? RequesterName,
    string? RequesterContact,
    RequestType? RequestType,
    DateOnly? RequestDate,
    DateOnly? DueDate,
    string? Notes,
    IReadOnlyList<string> Warnings,
    string ModelName);

/// <summary>The request form's text limits (CreateProjectDto).</summary>
public static class RequestDraftLimits
{
    public const int Short = 200;
    public const int Long = 2000;
    public const int MaxEmail = 20_000;
}
