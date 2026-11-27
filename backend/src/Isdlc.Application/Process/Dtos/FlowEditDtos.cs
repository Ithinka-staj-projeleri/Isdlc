using System.ComponentModel.DataAnnotations;
using Isdlc.Domain.Identity;
using Isdlc.Domain.Process;
using Isdlc.Domain.Projects;

namespace Isdlc.Application.Process.Dtos;

// Flow editing (Faz 7 · A7): what the company administrator sends. Every request carries the flow's RowVersion
// (from ProcessFlowDto); a flow saved by someone else meanwhile answers 409. Contract: docs/configurability.md → Akış düzenleme.

/// <summary>The flow's version the screen was showing.</summary>
public class FlowVersionDto
{
    [Required(ErrorMessage = "Akışın sürümü gönderilmelidir.")]
    public string RowVersion { get; set; } = string.Empty;
}

/// <summary>PUT /process-flows/{code}: the flow's name and which records it starts.</summary>
public class UpdateFlowDto : FlowVersionDto
{
    [Required(ErrorMessage = "Akışın adı zorunludur.")]
    [MaxLength(100, ErrorMessage = "Akışın adı en fazla 100 karakter olabilir.")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Null: the flow starts no records.</summary>
    public ProcessFlowUse? UsedFor { get; set; }
}

/// <summary>POST /process-flows/{code}/copy: a new flow of the company made from this one.</summary>
public class CopyFlowDto
{
    [Required(ErrorMessage = "Akışın kodu zorunludur.")]
    [RegularExpression("^[a-z0-9-]{2,50}$",
        ErrorMessage = "Akış kodu küçük harf, rakam ve tireden oluşmalıdır (2–50 karakter).")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Akışın adı zorunludur.")]
    [MaxLength(100, ErrorMessage = "Akışın adı en fazla 100 karakter olabilir.")]
    public string Name { get; set; } = string.Empty;
}

/// <summary>POST /process-flows/{code}/stages and PUT /process-stages/{id}.</summary>
public class SaveStageDto : FlowVersionDto
{
    [Required(ErrorMessage = "Aşamanın başlığı zorunludur.")]
    [MaxLength(100, ErrorMessage = "Başlık en fazla 100 karakter olabilir.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Aşamanın kısa başlığı zorunludur.")]
    [MaxLength(50, ErrorMessage = "Kısa başlık en fazla 50 karakter olabilir.")]
    public string ShortTitle { get; set; } = string.Empty;
}

/// <summary>A "No" answer that returns to an earlier step of the same flow.</summary>
public class StepJumpDto
{
    [Required(ErrorMessage = "Dönülecek adım seçilmelidir.")]
    public int? TargetStepId { get; set; }

    [Required(ErrorMessage = "Dönüşün açıklaması zorunludur.")]
    [MaxLength(200, ErrorMessage = "Açıklama en fazla 200 karakter olabilir.")]
    public string Label { get; set; } = string.Empty;
}

/// <summary>PUT /process-steps/{id}: everything about a step but its place in the flow.</summary>
public class SaveStepDto : FlowVersionDto
{
    [Required(ErrorMessage = "Adımın açıklaması zorunludur.")]
    [MaxLength(500, ErrorMessage = "Açıklama en fazla 500 karakter olabilir.")]
    public string Text { get; set; } = string.Empty;

    public bool IsDecision { get; set; }

    [MaxLength(500, ErrorMessage = "En fazla 500 karakter olabilir.")]
    public string? NoBranchText { get; set; }

    [Required(ErrorMessage = "Adımı kimin yapacağı seçilmelidir.")]
    public Permission? ResponsiblePermission { get; set; }

    /// <summary>Only on a decision step: then only someone with this permission answers it.</summary>
    public Permission? ApproverPermission { get; set; }

    /// <summary>A "No" that closes the record (Rejected, Lost, Redirected); a decision step has this or NoJumps, not both.</summary>
    public ProjectStatus? NoOutcome { get; set; }

    public List<StepJumpDto> NoJumps { get; set; } = [];

    public List<StepConditionRule> Conditions { get; set; } = [];

    public List<StepPrerequisite> Prerequisites { get; set; } = [];
}

/// <summary>POST /process-flows/{code}/steps: a new step in a stage, after a step of it or first in it.</summary>
public class AddStepDto : SaveStepDto
{
    [Required(ErrorMessage = "Aşama seçilmelidir.")]
    public int? StageId { get; set; }

    /// <summary>The step it comes after; null: first in the stage.</summary>
    public int? AfterStepId { get; set; }
}

/// <summary>PUT /process-flows/{code}/order: every stage of the flow in its new order, each with its steps in order.</summary>
public class ReorderFlowDto : FlowVersionDto
{
    [Required(ErrorMessage = "Aşamaların sırası gönderilmelidir.")]
    public List<StageOrderDto> Stages { get; set; } = [];
}

public class StageOrderDto
{
    [Required(ErrorMessage = "Aşama seçilmelidir.")]
    public int? Id { get; set; }

    public List<int> StepIds { get; set; } = [];
}
