using System.ComponentModel.DataAnnotations;
using Isdlc.Domain.Risks;

namespace Isdlc.Application.Agents.Dtos;

/// <summary>POST .../risks/suggestions — a text to find risks in (meeting notes, an e-mail, a document).</summary>
public record SuggestRisksDto
{
    [Required(ErrorMessage = "Metin zorunludur.")]
    [MaxLength(RiskSuggestionLimits.MaxSourceText, ErrorMessage = "Metin en fazla 20000 karakter olabilir.")]
    public string? SourceText { get; init; }
}

/// <summary>POST .../risks/assessment-suggestion — the risk a person is typing in the form.</summary>
public record SuggestRiskAssessmentDto
{
    [Required(ErrorMessage = "Risk tanımı zorunludur.")]
    [MaxLength(300, ErrorMessage = "Risk tanımı en fazla 300 karakter olabilir.")]
    public string? Title { get; init; }

    [MaxLength(2000, ErrorMessage = "Açıklama en fazla 2000 karakter olabilir.")]
    public string? Description { get; init; }
}

public static class RiskSuggestionLimits
{
    public const int MaxSourceText = 20_000;
}

/// <summary>
/// Risks the agent found. Nothing is saved: the person opens a suggestion in the risk form, edits it and saves it.
/// </summary>
public record RiskSuggestionsDto(
    IReadOnlyList<SuggestedRiskDto> Items,
    int SkippedCount, // risks the model returned that did not pass validation; only counted
    string ModelName);

/// <summary>
/// One suggested risk. Category is null when the model gave none; probability, impact, score and severity are null
/// when the model did not score it (the warnings say why). Score and severity follow RiskRules.
/// </summary>
public record SuggestedRiskDto(
    string Title,
    string? Description,
    RiskCategory? Category,
    int? Probability,
    int? Impact,
    int? Score,
    RiskSeverity? Severity,
    string? AssessmentReason,
    string? Mitigation,
    string? Evidence,
    IReadOnlyList<string> Warnings);

/// <summary>Suggested probability, impact and preventive action for the risk in the form.</summary>
public record RiskAssessmentSuggestionDto(
    int? Probability,
    int? Impact,
    int? Score,
    RiskSeverity? Severity,
    string? AssessmentReason,
    string? Mitigation,
    IReadOnlyList<string> Warnings,
    string ModelName);
