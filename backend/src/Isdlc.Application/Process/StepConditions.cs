using Isdlc.Domain.Projects;

namespace Isdlc.Application.Process;

/// <summary>A step's conditions. All false / null: the step is done on every record.</summary>
public record StepCondition(bool RequiresExternalRequest, bool RequiresOffer, int? MinLevel);

/// <summary>The record's values the conditions are checked against.</summary>
public record ConditionContext(RequestSource Source, bool RequiresOffer, int? Level);

/// <summary>A step as the rules need it: its order in the flow and its conditions.</summary>
public record ConditionalStep(int Id, int Order, StepCondition Condition);

/// <summary>
/// Pure rules for steps that are only done on some records. No database access, so every combination is unit tested.
/// Rules: docs/process-api.md → "Adım koşulları".
/// </summary>
public static class StepConditions
{
    /// <summary>
    /// Whether the step is done on the record. A level condition counts as met while the level is not decided yet:
    /// the level is set before the steps that depend on it, and skipping too early would lose a step.
    /// </summary>
    public static bool Applies(StepCondition condition, ConditionContext context) =>
        (!condition.RequiresExternalRequest || context.Source == RequestSource.External)
        && (!condition.RequiresOffer || context.RequiresOffer)
        && (condition.MinLevel is not { } minLevel || context.Level is not { } level || level >= minLevel);

    /// <summary>The first step of the flow that is done on the record; null if none is.</summary>
    public static int? FirstStepId(IEnumerable<ConditionalStep> flowSteps, ConditionContext context) =>
        flowSteps
            .OrderBy(s => s.Order)
            .FirstOrDefault(s => Applies(s.Condition, context))
            ?.Id;

    /// <summary>The first later step that is done on the record; null after the last one (the record closes).</summary>
    public static int? NextStepId(IEnumerable<ConditionalStep> flowSteps, int currentOrder, ConditionContext context) =>
        flowSteps
            .Where(s => s.Order > currentOrder)
            .OrderBy(s => s.Order)
            .FirstOrDefault(s => Applies(s.Condition, context))
            ?.Id;

    /// <summary>Steps of the flow that are skipped on the record, in flow order.</summary>
    public static IReadOnlyList<int> SkippedStepIds(IEnumerable<ConditionalStep> flowSteps, ConditionContext context) =>
        flowSteps
            .Where(s => !Applies(s.Condition, context))
            .OrderBy(s => s.Order)
            .Select(s => s.Id)
            .ToList();
}
