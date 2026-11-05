using System.ComponentModel.DataAnnotations;
using Isdlc.Application.Common.Models;
using Isdlc.Domain.Projects;

namespace Isdlc.Application.Projects.Dtos;

/// <summary>Where an active project stands: the current step and its stage.</summary>
public record CurrentStepSummaryDto(int Id, string Code, int StageOrder, string StageShortTitle);

/// <summary>One row of GET /api/v1/projects: only what the list screen shows.</summary>
public record ProjectListItemDto(
    int Id,
    string Code,
    string Name,
    string CustomerName,
    RequestType RequestType,
    ProjectStatus Status,
    CurrentStepSummaryDto? CurrentStep, // null when the project is not active
    UserSummaryDto? BusinessAnalyst,
    int? Level,
    ProjectPriority? Priority,
    DateOnly StartDate,
    DateOnly? DueDate);

/// <summary>
/// Where an active project is in the flow. The "Talepler" and "Projeler" menus are the same list with this filter.
/// </summary>
public enum ProjectPhase
{
    /// <summary>A request: the current step is in stages 1–3 (before the project is accepted).</summary>
    Request,

    /// <summary>An accepted project: the current step is in stages 4–13.</summary>
    Project,
}

/// <summary>List filters, all optional: ?status=Active&amp;phase=Request&amp;search=portal&amp;businessAnalystId=1</summary>
public record ProjectFilter
{
    public ProjectStatus? Status { get; init; }

    public ProjectPhase? Phase { get; init; }

    /// <summary>Text found in the code, project name or customer name.</summary>
    public string? Search { get; init; }

    public int? BusinessAnalystId { get; init; }
}

/// <summary>GET /api/v1/projects/{projectId}. Contract: docs/projects-api.md</summary>
public record ProjectDto(
    int Id,
    string Code,
    string Name,
    string Description,
    RequestSource Source,
    RequestChannel RequestChannel,
    RequestType RequestType,
    string CustomerName,
    string RequesterName,
    bool RequiresNda,
    ProjectStatus Status,
    CurrentStepSummaryDto? CurrentStep, // null when the project is not active
    UserSummaryDto? BusinessAnalyst,
    UserSummaryDto? ProjectManager,
    int? Level,
    ProjectPriority? Priority,
    DateOnly StartDate,
    DateOnly? DueDate,
    UserSummaryDto CreatedBy,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>
/// POST /api/v1/projects — a new request always starts Active at step 1.1.
/// Status, current step, priority and level are not here: Process and Assessment own them.
/// </summary>
public record CreateProjectDto : IValidatableObject
{
    [Required(ErrorMessage = "Proje adı zorunludur.")]
    [MaxLength(200, ErrorMessage = "Proje adı en fazla 200 karakter olabilir.")]
    public string? Name { get; init; }

    [Required(ErrorMessage = "Açıklama zorunludur.")]
    [MaxLength(2000, ErrorMessage = "Açıklama en fazla 2000 karakter olabilir.")]
    public string? Description { get; init; }

    // Enums are nullable so a missing value is an error instead of silently becoming the first value.
    [Required(ErrorMessage = "Talep kaynağı zorunludur.")]
    public RequestSource? Source { get; init; }

    [Required(ErrorMessage = "Talep kanalı zorunludur.")]
    public RequestChannel? RequestChannel { get; init; }

    [Required(ErrorMessage = "Talep türü zorunludur.")]
    public RequestType? RequestType { get; init; }

    [Required(ErrorMessage = "Müşteri adı zorunludur.")]
    [MaxLength(200, ErrorMessage = "Müşteri adı en fazla 200 karakter olabilir.")]
    public string? CustomerName { get; init; }

    [Required(ErrorMessage = "Talep sahibi zorunludur.")]
    [MaxLength(200, ErrorMessage = "Talep sahibi en fazla 200 karakter olabilir.")]
    public string? RequesterName { get; init; }

    public bool RequiresNda { get; init; }

    /// <summary>Optional; must be an active Business Analyst.</summary>
    public int? BusinessAnalystId { get; init; }

    /// <summary>Optional; must be an active Project Manager.</summary>
    public int? ProjectManagerId { get; init; }

    [Required(ErrorMessage = "Başlangıç tarihi zorunludur.")]
    public DateOnly? StartDate { get; init; }

    public DateOnly? DueDate { get; init; }

    /// <summary>Rules that involve more than one field (shared by create and update).</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartDate is { } start && DueDate is { } due && due < start)
        {
            yield return new ValidationResult(
                "Bitiş tarihi başlangıç tarihinden önce olamaz.", [nameof(DueDate)]);
        }
    }
}

/// <summary>
/// PUT /api/v1/projects/{projectId} — the same fields and rules as a new request.
/// Code, status, current step, priority and level never change here.
/// </summary>
public record UpdateProjectDto : CreateProjectDto;
