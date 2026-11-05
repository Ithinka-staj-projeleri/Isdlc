using Isdlc.Domain.Projects;

namespace Isdlc.Application.Process;

/// <summary>Where the project goes after an action: its new status and current step.</summary>
public record ProcessTransition(ProjectStatus Status, int? CurrentStepId);

/// <summary>
/// Pure rules for moving a project through the flow. No database access, so they can be unit tested directly.
/// Rules: docs/data-model.md → "Süreç ilerleme kuralları".
/// </summary>
public static class ProcessTransitions
{
    /// <summary>
    /// A non-decision step was completed: the project moves to the next step in flow order.
    /// After the last step (13.5) there is no next step, so the project is closed.
    /// </summary>
    public static ProcessTransition AfterCompleted(int? nextStepId) => nextStepId is null
        ? new ProcessTransition(ProjectStatus.Closed, null)
        : new ProcessTransition(ProjectStatus.Active, nextStepId);
}
