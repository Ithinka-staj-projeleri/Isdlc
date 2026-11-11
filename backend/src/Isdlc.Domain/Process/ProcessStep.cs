using Isdlc.Domain.Identity;
using Isdlc.Domain.Projects;

namespace Isdlc.Domain.Process;

/// <summary>
/// A step inside a stage. Master data loaded from the seed; not edited by users.
/// </summary>
public class ProcessStep
{
    public int Id { get; set; }

    public int ProcessStageId { get; set; }
    public ProcessStage ProcessStage { get; set; } = null!;

    /// <summary>The stage's flow, also kept on the step for the unique indexes and the "next step" query.</summary>
    public int ProcessFlowId { get; set; }
    public ProcessFlow ProcessFlow { get; set; } = null!;

    /// <summary>e.g. "4.7" or "12"; unique within the flow.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Position in the flow; unique within the flow. The next step is the first later one whose conditions hold.</summary>
    public int Order { get; set; }

    // Conditions: the step is skipped unless all of them hold for the record (StepConditions).

    /// <summary>Done only for external requests.</summary>
    public bool RequiresExternalRequest { get; set; }

    /// <summary>Done only when an offer will be prepared (Project.RequiresOffer).</summary>
    public bool RequiresOffer { get; set; }

    /// <summary>Done only when Project.Level is at least this, or not decided yet.</summary>
    public int? MinLevel { get; set; }

    public string Text { get; set; } = string.Empty;

    /// <summary>Whether the step is a Yes/No question.</summary>
    public bool IsDecision { get; set; }

    /// <summary>What happens on "No".</summary>
    public string? NoBranchText { get; set; }

    public UserRole ResponsibleRole { get; set; }

    /// <summary>Only set on approval steps. Then only this role can close the step.</summary>
    public UserRole? ApproverRole { get; set; }

    /// <summary>Set when "No" closes the request.</summary>
    public ProjectStatus? NoOutcome { get; set; }

    /// <summary>Steps the flow can return to on "No".</summary>
    public ICollection<ProcessStepJump> NoJumps { get; set; } = new List<ProcessStepJump>();
}
