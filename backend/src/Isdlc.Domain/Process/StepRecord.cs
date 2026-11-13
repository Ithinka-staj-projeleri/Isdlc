using Isdlc.Domain.Common;
using Isdlc.Domain.Identity;
using Isdlc.Domain.Projects;

namespace Isdlc.Domain.Process;

/// <summary>
/// One action taken on a step of a project. A project can have several records
/// for the same step (when the flow goes back). Records are never edited or deleted.
/// </summary>
public class StepRecord : AuditableEntity
{
    public int Id { get; set; }

    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public int ProcessStepId { get; set; }
    public ProcessStep ProcessStep { get; set; } = null!;

    public StepAction Action { get; set; }

    /// <summary>Only set on a "No" record that went back to an earlier step.</summary>
    public int? JumpedToStepId { get; set; }
    public ProcessStep? JumpedToStep { get; set; }

    /// <summary>For Rejected and Postponed: the explanation of the decision (RequestDecision.Explanation).</summary>
    public string? Note { get; set; }

    public int PerformedById { get; set; }
    public User PerformedBy { get; set; } = null!;

    public DateTime PerformedAt { get; set; }
}
