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

    /// <summary>e.g. "4.7", unique.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Position in the whole flow. The next step has Order + 1.</summary>
    public int Order { get; set; }

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
