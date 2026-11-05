using Isdlc.Domain.Projects;

namespace Isdlc.Application.Process;

/// <summary>
/// Where the project goes after an action: its new status and current step.
/// JumpedToStepId is set only when a "No" answer goes back to an earlier step; it is saved on the StepRecord.
/// </summary>
public record ProcessTransition(ProjectStatus Status, int? CurrentStepId, int? JumpedToStepId = null);

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

    /// <summary>"Yes" moves on exactly like completing a step.</summary>
    public static ProcessTransition AfterYes(int? nextStepId) => AfterCompleted(nextStepId);

    /// <summary>
    /// "No" has three outcomes, depending on the step:
    /// it closes the project (1.2, 3.2, 5.3), goes back to the chosen step (4.7, 5.4, ...),
    /// or leaves the project on the same step until it is answered again (2.2, 4.5, ...).
    /// Call ValidateNoAnswer first.
    /// </summary>
    public static ProcessTransition AfterNo(int currentStepId, ProjectStatus? noOutcome, int? jumpTargetStepId)
    {
        if (noOutcome is { } outcome)
        {
            return new ProcessTransition(outcome, null);
        }

        if (jumpTargetStepId is { } target)
        {
            return new ProcessTransition(ProjectStatus.Active, target, JumpedToStepId: target);
        }

        return new ProcessTransition(ProjectStatus.Active, currentStepId);
    }

    /// <summary>The manager rejected the request: it is closed as Rejected, whatever step it was on.</summary>
    public static ProcessTransition AfterRejected() => new(ProjectStatus.Rejected, null);

    /// <summary>
    /// Checks the chosen return step of a "No" answer. Returns the error message, or null when it is valid.
    /// A step with return options needs one of them; a step without options must not get one.
    /// </summary>
    public static string? ValidateNoAnswer(IReadOnlyCollection<int> jumpTargetStepIds, int? jumpTargetStepId)
    {
        if (jumpTargetStepIds.Count > 0)
        {
            return jumpTargetStepId is { } target && jumpTargetStepIds.Contains(target)
                ? null
                : "Geçerli bir dönüş seçeneği seçilmelidir.";
        }

        return jumpTargetStepId is null ? null : "Bu adımda dönüş seçeneği yok; dönüş adımı seçilmez.";
    }
}
