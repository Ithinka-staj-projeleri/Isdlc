using System.ComponentModel.DataAnnotations;
using Isdlc.Application.Common.Models;
using Isdlc.Domain.Projects;

namespace Isdlc.Application.Projects.Dtos;

/// <summary>The project an add-on belongs to.</summary>
public record ParentProjectDto(int Id, string Code, string Name);

/// <summary>Where an active project stands: the current step and its stage.</summary>
public record CurrentStepSummaryDto(int Id, string Code, int StageOrder, string StageShortTitle);

/// <summary>One row of GET /api/v1/projects: only what the list screen shows.</summary>
public record ProjectListItemDto(
    int Id,
    string Code, // Proje No
    string Name,
    string CustomerName,
    RequestType RequestType,
    ProjectStatus Status,
    CurrentStepSummaryDto? CurrentStep, // null when the project is not active
    UserSummaryDto? BusinessAnalyst,
    int? Level,
    ProjectPriority? Priority,
    DateOnly RequestDate,
    DateOnly? DueDate);

/// <summary>
/// Splits every record in two. The "Talepler" and "Projeler" menus are the same list with this filter;
/// every record appears on exactly one of them.
/// </summary>
public enum ProjectPhase
{
    /// <summary>A waiting request: active, current step before the flow's project stage (docs/process-api.md).</summary>
    Request,

    /// <summary>
    /// Everything that has left the request phase: active records in the project stages and all closed records
    /// (closed, rejected, lost, redirected). Combine with status=Active for active projects only.
    /// </summary>
    Project,
}

/// <summary>List filters, all optional: ?status=Active&amp;phase=Request&amp;search=portal&amp;businessAnalystId=1</summary>
public record ProjectFilter
{
    public ProjectStatus? Status { get; init; }

    public ProjectPhase? Phase { get; init; }

    /// <summary>The exact Proje No, or text found in the project name or customer name.</summary>
    public string? Search { get; init; }

    public int? BusinessAnalystId { get; init; }

    /// <summary>Only the add-ons of this project (its "Ek Geliştirmeler" tab).</summary>
    public int? ParentProjectId { get; init; }

    /// <summary>
    /// true: only projects an add-on can be opened on (the request form's project list): new software projects that
    /// have been accepted (active in a project stage) or are closed.
    /// </summary>
    public bool? AcceptsAddOns { get; init; }
}

/// <summary>GET /api/v1/projects/{projectId}. Contract: docs/projects-api.md</summary>
public record ProjectDto(
    int Id,
    string Code,
    string Name,
    string Description,
    ParentProjectDto? ParentProject, // only for an add-on
    RequestSource Source,
    RequestChannel RequestChannel,
    string? RequestChannelOther, // only for RequestChannel.Other
    RequestType RequestType,
    string CustomerName,
    string RequesterName,
    string? RequesterContact,
    string? Notes,
    bool RequiresOffer,
    bool RequiresNda,
    ProjectStatus Status,
    CurrentStepSummaryDto? CurrentStep, // null when the project is not active
    UserSummaryDto? BusinessAnalyst,
    UserSummaryDto? ProjectManager,
    int? Level,
    ProjectPriority? Priority,
    DateOnly RequestDate,
    DateOnly? DueDate,
    UserSummaryDto CreatedBy, // talebi kayda alan
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>
/// POST /api/v1/projects — a new request always starts Active at the first step of its flow.
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

    /// <summary>Required when the channel is "Diğer"; ignored for the other channels.</summary>
    [MaxLength(200, ErrorMessage = "Diğer talep kanalı en fazla 200 karakter olabilir.")]
    public string? RequestChannelOther { get; init; }

    [Required(ErrorMessage = "Talep türü zorunludur.")]
    public RequestType? RequestType { get; init; }

    /// <summary>
    /// Ek geliştirme: the project the request belongs to. Required exactly when RequestType is AdditionalModule.
    /// Never changes after the request is opened (an update must send the same value).
    /// </summary>
    public int? ParentProjectId { get; init; }

    [Required(ErrorMessage = "Talep sahibi zorunludur.")]
    [MaxLength(200, ErrorMessage = "Talep sahibi en fazla 200 karakter olabilir.")]
    public string? CustomerName { get; init; }

    [Required(ErrorMessage = "Talebi ileten kişi zorunludur.")]
    [MaxLength(200, ErrorMessage = "Talebi ileten kişi en fazla 200 karakter olabilir.")]
    public string? RequesterName { get; init; }

    /// <summary>E-mail or phone; the format is not checked.</summary>
    [MaxLength(200, ErrorMessage = "İletişim bilgisi en fazla 200 karakter olabilir.")]
    public string? RequesterContact { get; init; }

    [MaxLength(2000, ErrorMessage = "Notlar en fazla 2000 karakter olabilir.")]
    public string? Notes { get; init; }

    /// <summary>Nullable so a missing answer is an error instead of silently becoming false.</summary>
    [Required(ErrorMessage = "Teklif hazırlanıp hazırlanmayacağı belirtilmelidir.")]
    public bool? RequiresOffer { get; init; }

    public bool RequiresNda { get; init; }

    /// <summary>Optional; must be an active Business Analyst.</summary>
    public int? BusinessAnalystId { get; init; }

    /// <summary>Optional; must be an active Project Manager.</summary>
    public int? ProjectManagerId { get; init; }

    [Required(ErrorMessage = "Talep tarihi zorunludur.")]
    public DateOnly? RequestDate { get; init; }

    public DateOnly? DueDate { get; init; }

    /// <summary>Rules that involve more than one field (shared by create and update).</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (RequestType == Domain.Projects.RequestType.AdditionalModule && ParentProjectId is null)
        {
            yield return new ValidationResult(
                "Ek geliştirmenin ait olduğu proje seçilmelidir.", [nameof(ParentProjectId)]);
        }
        else if (RequestType is { } type && type != Domain.Projects.RequestType.AdditionalModule && ParentProjectId is not null)
        {
            yield return new ValidationResult(
                "Bir projeye bağlı talep ek geliştirmedir; talep türü \"Ek geliştirme\" olmalıdır.", [nameof(RequestType)]);
        }

        if (RequestChannel == Domain.Projects.RequestChannel.Other && string.IsNullOrWhiteSpace(RequestChannelOther))
        {
            yield return new ValidationResult(
                "Diğer talep kanalı yazılmalıdır.", [nameof(RequestChannelOther)]);
        }

        if (RequestDate is { } requested && DueDate is { } due && due < requested)
        {
            yield return new ValidationResult(
                "Bitiş tarihi talep tarihinden önce olamaz.", [nameof(DueDate)]);
        }
    }
}

/// <summary>
/// PUT /api/v1/projects/{projectId} — the same fields and rules as a new request.
/// Proje No, status, current step, priority and level never change here. Source and RequiresOffer change only
/// until the record reaches the first step that depends on them (409 afterwards).
/// </summary>
public record UpdateProjectDto : CreateProjectDto;
