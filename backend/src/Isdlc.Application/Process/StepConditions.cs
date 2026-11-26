using System.Globalization;
using Isdlc.Domain.Process;
using Isdlc.Domain.Projects;

namespace Isdlc.Application.Process;

/// <summary>
/// The record's values the rules are checked against. Custom fields (Faz 7 · A6) by key; empty until the company has any.
/// </summary>
public record ConditionContext(
    RequestSource Source,
    RequestType RequestType,
    bool RequiresOffer,
    int? Level,
    IReadOnlyDictionary<string, string>? CustomValues = null);

/// <summary>A step as the rules need it: its order in the flow and its rules.</summary>
public record ConditionalStep(int Id, int Order, IReadOnlyList<StepConditionRule> Conditions);

/// <summary>
/// Pure rules for steps that are only done on some records (Faz 7 · A5). No database access, so every combination is
/// unit tested. Rules: docs/configurability.md → Atlama kuralları.
/// </summary>
public static class StepConditions
{
    /// <summary>Whether the step is done on the record: all its rules hold. No rules: on every record.</summary>
    public static bool Applies(IEnumerable<StepConditionRule> conditions, ConditionContext context) =>
        conditions.All(rule => Holds(rule, context));

    /// <summary>
    /// One rule. A field the record does not have (unknown name, a custom field it has no value for) is empty;
    /// an empty field gives the rule's WhenEmpty, except for IsSet / IsNotSet which ask exactly that.
    /// </summary>
    public static bool Holds(StepConditionRule rule, ConditionContext context)
    {
        var value = ValueOf(rule.Field, context);

        switch (rule.Op)
        {
            case ConditionOperator.IsSet:
                return value is not null;
            case ConditionOperator.IsNotSet:
                return value is null;
        }

        if (value is null)
        {
            return rule.WhenEmpty == ConditionWhenEmpty.Met;
        }

        if (rule.Value is null)
        {
            return false;
        }

        return rule.Op switch
        {
            ConditionOperator.Equals => SameValue(value, rule.Value),
            ConditionOperator.NotEquals => !SameValue(value, rule.Value),
            ConditionOperator.AtLeast => Compare(value, rule.Value) is >= 0,
            ConditionOperator.AtMost => Compare(value, rule.Value) is <= 0,
            _ => false,
        };
    }

    /// <summary>Whether any rule looks at the field: the request source and the offer cannot change after such a step.</summary>
    public static bool DependsOn(IEnumerable<StepConditionRule> conditions, string field) =>
        conditions.Any(rule => rule.Field == field);

    /// <summary>The first step of the flow that is done on the record; null if none is.</summary>
    public static int? FirstStepId(IEnumerable<ConditionalStep> flowSteps, ConditionContext context) =>
        flowSteps
            .OrderBy(s => s.Order)
            .FirstOrDefault(s => Applies(s.Conditions, context))
            ?.Id;

    /// <summary>The first later step that is done on the record; null after the last one (the record closes).</summary>
    public static int? NextStepId(IEnumerable<ConditionalStep> flowSteps, int currentOrder, ConditionContext context) =>
        flowSteps
            .Where(s => s.Order > currentOrder)
            .OrderBy(s => s.Order)
            .FirstOrDefault(s => Applies(s.Conditions, context))
            ?.Id;

    /// <summary>Steps of the flow that are skipped on the record, in flow order.</summary>
    public static IReadOnlyList<int> SkippedStepIds(IEnumerable<ConditionalStep> flowSteps, ConditionContext context) =>
        flowSteps
            .Where(s => !Applies(s.Conditions, context))
            .OrderBy(s => s.Order)
            .Select(s => s.Id)
            .ToList();

    /// <summary>
    /// The rules as the screens show them, one label per rule. The three rules of Faz 4 keep their old labels
    /// ("Yalnızca dış talepte", "Teklif hazırlanacaksa", "Seviye 2 ve üstü").
    /// </summary>
    public static IReadOnlyList<string> Describe(IEnumerable<StepConditionRule> conditions) =>
        conditions.Select(Describe).ToList();

    public static string Describe(StepConditionRule rule) => rule switch
    {
        { Field: StepConditionRule.Source, Op: ConditionOperator.Equals, Value: "External" } => "Yalnızca dış talepte",
        { Field: StepConditionRule.RequiresOffer, Op: ConditionOperator.Equals, Value: "true" } => "Teklif hazırlanacaksa",
        { Field: StepConditionRule.Level, Op: ConditionOperator.AtLeast } => $"Seviye {rule.Value} ve üstü",
        { Op: ConditionOperator.IsSet } => $"{FieldName(rule.Field)} doluysa",
        { Op: ConditionOperator.IsNotSet } => $"{FieldName(rule.Field)} boşsa",
        _ => $"{FieldName(rule.Field)} {OperatorText(rule.Op)} {ValueName(rule.Field, rule.Value)}",
    };

    private static string? ValueOf(string field, ConditionContext context) => field switch
    {
        StepConditionRule.Source => context.Source.ToString(),
        StepConditionRule.RequestType => context.RequestType.ToString(),
        StepConditionRule.RequiresOffer => context.RequiresOffer ? "true" : "false",
        StepConditionRule.Level => context.Level?.ToString(CultureInfo.InvariantCulture),
        _ when field.StartsWith(StepConditionRule.CustomPrefix, StringComparison.Ordinal)
            && context.CustomValues is { } custom
            && custom.TryGetValue(field[StepConditionRule.CustomPrefix.Length..], out var value)
            && !string.IsNullOrWhiteSpace(value) => value,
        _ => null,
    };

    private static bool SameValue(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>Numbers as numbers, dates (yyyy-MM-dd) as dates; anything else cannot be compared (null).</summary>
    private static int? Compare(string value, string expected)
    {
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var a)
            && decimal.TryParse(expected, NumberStyles.Number, CultureInfo.InvariantCulture, out var b))
        {
            return a.CompareTo(b);
        }

        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d1)
            && DateOnly.TryParseExact(expected, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d2))
        {
            return d1.CompareTo(d2);
        }

        return null;
    }

    private static string FieldName(string field) => field switch
    {
        StepConditionRule.Source => "Talep kaynağı",
        StepConditionRule.RequestType => "Talep türü",
        StepConditionRule.RequiresOffer => "Teklif",
        StepConditionRule.Level => "Seviye",
        _ when field.StartsWith(StepConditionRule.CustomPrefix, StringComparison.Ordinal) =>
            field[StepConditionRule.CustomPrefix.Length..],
        _ => field,
    };

    private static string OperatorText(ConditionOperator op) => op switch
    {
        ConditionOperator.Equals => "=",
        ConditionOperator.NotEquals => "≠",
        ConditionOperator.AtLeast => "≥",
        ConditionOperator.AtMost => "≤",
        _ => op.ToString(),
    };

    private static string ValueName(string field, string? value) => (field, value) switch
    {
        (StepConditionRule.Source, "Internal") => "Kurum içi",
        (StepConditionRule.Source, "External") => "Dış (müşteri)",
        (StepConditionRule.Source, "Affiliated") => "İlişkili şirket",
        (StepConditionRule.RequiresOffer, "true") => "hazırlanacak",
        (StepConditionRule.RequiresOffer, "false") => "hazırlanmayacak",
        (_, "true") => "Evet",
        (_, "false") => "Hayır",
        _ => value ?? "",
    };
}
