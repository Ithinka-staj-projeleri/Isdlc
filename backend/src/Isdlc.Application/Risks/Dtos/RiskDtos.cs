using System.ComponentModel.DataAnnotations;
using Isdlc.Application.Common.Models;
using Isdlc.Domain.Risks;

namespace Isdlc.Application.Risks.Dtos;

public record RiskListItemDto(
    int Id,
    string Code,
    string Title,
    RiskCategory Category,
    int Probability,
    int Impact,
    int Score,
    string? Mitigation,
    UserSummaryDto Owner,
    bool IsSharedWithRequester,
    RiskStatus Status)
{
    /// <summary>Derived from the score; never stored.</summary>
    public RiskSeverity Severity => RiskRules.SeverityOf(Score);
}

public record RiskDto(
    int Id,
    int ProjectId,
    string Code,
    string Title,
    string? Description,
    RiskCategory Category,
    int Probability,
    int Impact,
    int Score,
    string? Mitigation,
    UserSummaryDto Owner,
    bool IsSharedWithRequester,
    RiskStatus Status,
    UserSummaryDto CreatedBy,
    DateTime CreatedAt,
    DateTime? UpdatedAt)
{
    /// <summary>Derived from the score; never stored.</summary>
    public RiskSeverity Severity => RiskRules.SeverityOf(Score);
}

/// <summary>
/// List filters, all optional: ?status=Open&amp;category=Technical&amp;severity=High.
/// probability and impact are sent together (a cell of the heat map).
/// </summary>
public record RiskFilter
{
    public RiskStatus? Status { get; init; }
    public RiskCategory? Category { get; init; }
    public RiskSeverity? Severity { get; init; }

    [Range(RiskRules.MinLevel, RiskRules.MaxLevel, ErrorMessage = "Olasılık 1 ile 5 arasında olmalıdır.")]
    public int? Probability { get; init; }

    [Range(RiskRules.MinLevel, RiskRules.MaxLevel, ErrorMessage = "Etki 1 ile 5 arasında olmalıdır.")]
    public int? Impact { get; init; }
}

/// <summary>One cell of the heat map: how many open or monitored risks have this probability and impact.</summary>
public record RiskMatrixCellDto(int Probability, int Impact, int Count);

public record RiskSeverityCountsDto(int Low, int Medium, int High);

/// <summary>Heat map of the risks that are not closed. Empty cells are not sent.</summary>
public record RiskMatrixDto(IReadOnlyList<RiskMatrixCellDto> Cells, RiskSeverityCountsDto CountBySeverity);

/// <summary>POST .../risks — a new risk always starts as Open.</summary>
public record CreateRiskDto
{
    [Required(ErrorMessage = "Risk tanımı zorunludur.")]
    [MaxLength(300, ErrorMessage = "Risk tanımı en fazla 300 karakter olabilir.")]
    public string? Title { get; init; }

    [MaxLength(2000, ErrorMessage = "Açıklama en fazla 2000 karakter olabilir.")]
    public string? Description { get; init; }

    // Nullable so a missing value is an error instead of silently becoming the enum's first value or 0.
    [Required(ErrorMessage = "Kategori seçilmelidir.")]
    public RiskCategory? Category { get; init; }

    [Required(ErrorMessage = "Olasılık 1 ile 5 arasında olmalıdır.")]
    [Range(RiskRules.MinLevel, RiskRules.MaxLevel, ErrorMessage = "Olasılık 1 ile 5 arasında olmalıdır.")]
    public int? Probability { get; init; }

    [Required(ErrorMessage = "Etki 1 ile 5 arasında olmalıdır.")]
    [Range(RiskRules.MinLevel, RiskRules.MaxLevel, ErrorMessage = "Etki 1 ile 5 arasında olmalıdır.")]
    public int? Impact { get; init; }

    [MaxLength(2000, ErrorMessage = "Önleyici aksiyon en fazla 2000 karakter olabilir.")]
    public string? Mitigation { get; init; }

    [Required(ErrorMessage = "Sorumlu seçilmelidir.")]
    public int? OwnerUserId { get; init; }

    public bool IsSharedWithRequester { get; init; }
}

/// <summary>PUT .../risks/{id} — the create fields plus the status (the status is part of the form).</summary>
public record UpdateRiskDto : CreateRiskDto
{
    [Required(ErrorMessage = "Durum zorunludur.")]
    public RiskStatus? Status { get; init; }
}
