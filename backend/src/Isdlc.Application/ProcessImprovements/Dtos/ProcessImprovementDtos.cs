using System.ComponentModel.DataAnnotations;
using Isdlc.Application.Common.Models;
using Isdlc.Domain.ProcessImprovements;
using Isdlc.Domain.Projects;

namespace Isdlc.Application.ProcessImprovements.Dtos;

// Contract: docs/process-improvement-api.md → DTO'lar

/// <summary>Where the proposal stands in its flow; null when the proposal is closed or rejected.</summary>
public record ImprovementStepDto(int Id, string Code, int StageOrder, string StageShortTitle);

/// <summary>A row of GET /process-improvements (newest first). Priority and score are null until scored.</summary>
public record ImprovementListItemDto(
    int Id,
    string Code,
    string Title,
    ImprovementSource Source,
    string AffectedProcess,
    ImprovementPriority? Priority,
    int? PriorityScore,
    ProjectStatus Status,
    ImprovementStepDto? CurrentStep,
    UserSummaryDto? Owner,
    DateTime CreatedAt);

public record ImprovementPrioritizationDto(int? Impact, int? Frequency, int? Ease, int? Score, ImprovementPriority? Priority);

public record ImprovementStudyDto(
    string? CurrentState, string? RootCause, string? Solution, string? PilotPlan, string? PilotTarget, string? CostBenefit);

public record PilotEvaluationDto(string? PilotActual, string? PilotFeedback, PilotResult? PilotResult, string? PilotDecision);

/// <summary>The proposal with its four sections. Id is the proposal's Project id.</summary>
public record ImprovementDto(
    int Id,
    string Code,
    ProjectStatus Status,
    ImprovementStepDto? CurrentStep,
    string Title,
    string Problem,
    ImprovementSource Source,
    string AffectedProcess,
    string? ProposedSolution,
    string SuggestedBy,
    UserSummaryDto? Owner,
    ImprovementPrioritizationDto Prioritization,
    ImprovementStudyDto Study,
    PilotEvaluationDto PilotEvaluation,
    UserSummaryDto CreatedBy,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>?status=Active&amp;stageOrder=1</summary>
public record ImprovementFilter
{
    public ProjectStatus? Status { get; init; }

    /// <summary>The stage (1 Planla, 2 Uygula, 3 Kontrol Et, 4 Önlem Al) the proposal is at.</summary>
    public int? StageOrder { get; init; }
}

/// <summary>PUT /process-improvements/{id} — section 1, the proposal itself.</summary>
public record UpdateImprovementDto
{
    [Required(ErrorMessage = "Başlık zorunludur.")]
    [MaxLength(200, ErrorMessage = "Başlık en fazla 200 karakter olabilir.")]
    public string? Title { get; init; }

    [Required(ErrorMessage = "Sorunun tanımı zorunludur.")]
    [MaxLength(2000, ErrorMessage = "Sorunun tanımı en fazla 2000 karakter olabilir.")]
    public string? Problem { get; init; }

    // Nullable so a missing value is an error instead of silently becoming the default.
    [Required(ErrorMessage = "Önerinin kaynağı seçilmelidir.")]
    public ImprovementSource? Source { get; init; }

    [Required(ErrorMessage = "Etkilediği süreç zorunludur.")]
    [MaxLength(ProcessImprovement.MaxShortText, ErrorMessage = "Etkilediği süreç en fazla 200 karakter olabilir.")]
    public string? AffectedProcess { get; init; }

    [MaxLength(ProcessImprovement.MaxProposedSolution, ErrorMessage = "Çözüm fikri en fazla 2000 karakter olabilir.")]
    public string? ProposedSolution { get; init; }

    [Required(ErrorMessage = "Öneriyi getiren kişi zorunludur.")]
    [MaxLength(ProcessImprovement.MaxShortText, ErrorMessage = "Öneriyi getiren en fazla 200 karakter olabilir.")]
    public string? SuggestedBy { get; init; }

    [Required(ErrorMessage = "Öneriyi yürütecek kişi seçilmelidir.")]
    public int? OwnerUserId { get; init; }
}

/// <summary>POST /process-improvements — the same fields; the proposal starts at the flow's first step.</summary>
public record CreateImprovementDto : UpdateImprovementDto;

/// <summary>PUT .../prioritization — all three or none (none empties the scores).</summary>
public record SavePrioritizationDto
{
    [Range(ProcessImprovement.MinScore, ProcessImprovement.MaxScore, ErrorMessage = "Puan 1 ile 5 arasında olmalıdır.")]
    public int? Impact { get; init; }

    [Range(ProcessImprovement.MinScore, ProcessImprovement.MaxScore, ErrorMessage = "Puan 1 ile 5 arasında olmalıdır.")]
    public int? Frequency { get; init; }

    [Range(ProcessImprovement.MinScore, ProcessImprovement.MaxScore, ErrorMessage = "Puan 1 ile 5 arasında olmalıdır.")]
    public int? Ease { get; init; }
}

/// <summary>PUT .../study — every field optional; an empty field is "not written yet".</summary>
public record SaveStudyDto
{
    [MaxLength(ProcessImprovement.MaxStudyText, ErrorMessage = "Mevcut durum en fazla 4000 karakter olabilir.")]
    public string? CurrentState { get; init; }

    [MaxLength(ProcessImprovement.MaxStudyText, ErrorMessage = "Kök neden en fazla 4000 karakter olabilir.")]
    public string? RootCause { get; init; }

    [MaxLength(ProcessImprovement.MaxStudyText, ErrorMessage = "Çözüm en fazla 4000 karakter olabilir.")]
    public string? Solution { get; init; }

    [MaxLength(ProcessImprovement.MaxStudyText, ErrorMessage = "Pilot planı en fazla 4000 karakter olabilir.")]
    public string? PilotPlan { get; init; }

    [MaxLength(ProcessImprovement.MaxPilotMeasure, ErrorMessage = "Pilot hedefi en fazla 1000 karakter olabilir.")]
    public string? PilotTarget { get; init; }

    [MaxLength(ProcessImprovement.MaxStudyText, ErrorMessage = "Fayda / maliyet en fazla 4000 karakter olabilir.")]
    public string? CostBenefit { get; init; }
}

/// <summary>PUT .../pilot-evaluation — every field optional.</summary>
public record SavePilotEvaluationDto
{
    [MaxLength(ProcessImprovement.MaxPilotMeasure, ErrorMessage = "Gerçekleşen en fazla 1000 karakter olabilir.")]
    public string? PilotActual { get; init; }

    [MaxLength(ProcessImprovement.MaxPilotFeedback, ErrorMessage = "Geri bildirim en fazla 4000 karakter olabilir.")]
    public string? PilotFeedback { get; init; }

    public PilotResult? PilotResult { get; init; }

    [MaxLength(ProcessImprovement.MaxPilotDecision, ErrorMessage = "Karar en fazla 2000 karakter olabilir.")]
    public string? PilotDecision { get; init; }
}
