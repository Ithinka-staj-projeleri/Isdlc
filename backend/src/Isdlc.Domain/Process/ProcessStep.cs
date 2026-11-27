using Isdlc.Domain.Common;
using Isdlc.Domain.Identity;
using Isdlc.Domain.Projects;

namespace Isdlc.Domain.Process;

/// <summary>
/// A step inside a stage. Master data loaded from the seed; not edited by users.
/// </summary>
public class ProcessStep : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }

    public int ProcessStageId { get; set; }
    public ProcessStage ProcessStage { get; set; } = null!;

    /// <summary>The stage's flow, also kept on the step for the unique indexes and the "next step" query.</summary>
    public int ProcessFlowId { get; set; }
    public ProcessFlow ProcessFlow { get; set; } = null!;

    /// <summary>e.g. "4.7" or "12"; unique within the flow.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Position in the flow; unique within the flow. The next step is the first later one whose conditions hold.</summary>
    public int Order { get; set; }

    /// <summary>
    /// Never deleted, so history keeps pointing at it: an inactive step is skipped by every record and hidden from the
    /// flow (Faz 7 · A7). Its code is "P{Id}".
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// When the step is done: on a record where all rules hold, skipped otherwise; empty, on every record (StepConditions).
    /// Replaces the three fixed conditions (external request, offer, level) of Faz 4 (Faz 7 · A5).
    /// </summary>
    public List<StepConditionRule> Conditions { get; set; } = [];

    public string Text { get; set; } = string.Empty;

    /// <summary>Whether the step is a Yes/No question.</summary>
    public bool IsDecision { get; set; }

    /// <summary>What happens on "No".</summary>
    public string? NoBranchText { get; set; }

    /// <summary>Who does the step: anyone with this permission (Faz 7 · A2).</summary>
    public Permission ResponsiblePermission { get; set; }

    /// <summary>Only set on approval steps. Then only someone with this permission can close the step.</summary>
    public Permission? ApproverPermission { get; set; }

    /// <summary>Set when "No" closes the request.</summary>
    public ProjectStatus? NoOutcome { get; set; }

    /// <summary>Work that must exist on the project before the step is completed or answered "Yes"; empty: none.</summary>
    public List<StepPrerequisite> Prerequisites { get; set; } = [];

    /// <summary>Steps the flow can return to on "No".</summary>
    public ICollection<ProcessStepJump> NoJumps { get; set; } = new List<ProcessStepJump>();
}
