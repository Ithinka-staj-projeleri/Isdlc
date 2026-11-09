using System.ComponentModel.DataAnnotations;
using Isdlc.Application.Common.Models;
using Isdlc.Domain.Requirements;

namespace Isdlc.Application.Requirements.Dtos;

public record RequirementListItemDto(
    int Id,
    string Code,
    RequirementType Type,
    string Title,
    RequirementPriority Priority,
    RequirementStatus Status);

public record RequirementDto(
    int Id,
    int ProjectId,
    string Code,
    RequirementType Type,
    string Title,
    string? Description,
    string? AcceptanceCriteria,
    RequirementPriority Priority,
    RequirementStatus Status,
    UserSummaryDto CreatedBy,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>List filters, all optional: ?status=Approved&amp;type=Functional&amp;priority=MustHave&amp;search=mobil</summary>
public record RequirementFilter
{
    public RequirementStatus? Status { get; init; }
    public RequirementType? Type { get; init; }
    public RequirementPriority? Priority { get; init; }

    /// <summary>Text in the code or the title.</summary>
    public string? Search { get; init; }
}

/// <summary>POST .../requirements — a new record always starts as Draft.</summary>
public record CreateRequirementDto
{
    // Nullable so a missing value is an error instead of silently becoming the enum's first value.
    [Required(ErrorMessage = "Gereksinim türü zorunludur.")]
    public RequirementType? Type { get; init; }

    [Required(ErrorMessage = "Başlık zorunludur.")]
    [MaxLength(300, ErrorMessage = "Başlık en fazla 300 karakter olabilir.")]
    public string? Title { get; init; }

    [MaxLength(4000, ErrorMessage = "Açıklama en fazla 4000 karakter olabilir.")]
    public string? Description { get; init; }

    [MaxLength(4000, ErrorMessage = "Kabul ölçütleri en fazla 4000 karakter olabilir.")]
    public string? AcceptanceCriteria { get; init; }

    [Required(ErrorMessage = "Öncelik zorunludur.")]
    public RequirementPriority? Priority { get; init; }
}

/// <summary>PUT .../requirements/{id} — same fields as create; the status is changed separately.</summary>
public record UpdateRequirementDto : CreateRequirementDto;

/// <summary>PUT .../requirements/{id}/status</summary>
public record UpdateRequirementStatusDto
{
    [Required(ErrorMessage = "Durum zorunludur.")]
    public RequirementStatus? Status { get; init; }
}
