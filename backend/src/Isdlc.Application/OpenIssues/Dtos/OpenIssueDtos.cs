using System.ComponentModel.DataAnnotations;
using Isdlc.Application.Common.Models;
using Isdlc.Domain.OpenIssues;

namespace Isdlc.Application.OpenIssues.Dtos;

/// <summary>Who is responsible: a team member (UserId set) or someone outside the system (UserId null).</summary>
public record OwnerDto(int? UserId, string Name);

public record OpenIssueListItemDto(
    int Id,
    string Code,
    OpenIssueType Type,
    string Title,
    OwnerDto Owner,
    DateOnly? DueDate,
    OpenIssueStatus Status,
    bool IsOverdue);

public record OpenIssueDto(
    int Id,
    int ProjectId,
    string Code,
    OpenIssueType Type,
    string Title,
    string? Description,
    OwnerDto Owner,
    DateOnly? DueDate,
    OpenIssueStatus Status,
    bool IsOverdue,
    DateTime? ClosedAt,
    UserSummaryDto CreatedBy,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>List filters, all optional: ?status=Open&amp;type=Action&amp;overdueOnly=true</summary>
public record OpenIssueFilter
{
    public OpenIssueStatus? Status { get; init; }
    public OpenIssueType? Type { get; init; }
    public bool OverdueOnly { get; init; }
}

/// <summary>POST .../open-issues — a new record always starts as Open.</summary>
public record CreateOpenIssueDto
{
    // Nullable so a missing type is an error instead of silently becoming the enum's first value.
    [Required(ErrorMessage = "Kayıt türü zorunludur.")]
    public OpenIssueType? Type { get; init; }

    [Required(ErrorMessage = "Konu zorunludur.")]
    [MaxLength(300, ErrorMessage = "Konu en fazla 300 karakter olabilir.")]
    public string? Title { get; init; }

    [MaxLength(2000, ErrorMessage = "Açıklama en fazla 2000 karakter olabilir.")]
    public string? Description { get; init; }

    /// <summary>A team member. Send exactly one of OwnerUserId and OwnerName.</summary>
    public int? OwnerUserId { get; init; }

    /// <summary>Someone outside the system. Send exactly one of OwnerUserId and OwnerName.</summary>
    [MaxLength(200, ErrorMessage = "Sorumlu adı en fazla 200 karakter olabilir.")]
    public string? OwnerName { get; init; }

    public DateOnly? DueDate { get; init; }
}

/// <summary>PUT .../open-issues/{id} — same fields as create; the status is changed separately.</summary>
public record UpdateOpenIssueDto : CreateOpenIssueDto;

/// <summary>PUT .../open-issues/{id}/status</summary>
public record UpdateOpenIssueStatusDto
{
    [Required(ErrorMessage = "Durum zorunludur.")]
    public OpenIssueStatus? Status { get; init; }
}
