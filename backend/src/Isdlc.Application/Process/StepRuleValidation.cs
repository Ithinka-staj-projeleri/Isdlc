using System.Globalization;
using Isdlc.Domain.Process;
using Isdlc.Domain.Projects;

namespace Isdlc.Application.Process;

/// <summary>The kind of value a skip rule's field holds; decides which comparisons and values are allowed.</summary>
public enum ConditionFieldType
{
    Choice,
    Boolean,
    Number,
    Date,
    Text,
}

/// <summary>
/// A field a skip rule can look at: one of the record's own fields or a company's custom field ("custom:{key}", Faz 7 · A6).
/// Options: the allowed values of a choice.
/// </summary>
public record ConditionField(string Field, string Name, ConditionFieldType Type, IReadOnlyList<string>? Options = null);

/// <summary>An error of the rule or prerequisite at Index of the step's list; the API key is e.g. "conditions[0]".</summary>
public record StepRuleError(int Index, string Message);

/// <summary>
/// Whether a skip rule or a prerequisite can be saved on a step (Faz 7 · A7, flow editing). Pure, no database: the caller
/// passes the company's custom fields, form codes and document type codes. A rule that passes here can still stop holding
/// later (a custom field deactivated); StepConditions then treats it as not met.
/// Rules: docs/configurability.md → Atlama kuralları, Adım ön koşulları.
/// </summary>
public static class StepRuleValidation
{
    public const int MaxRules = 10;
    public const int MaxPrerequisites = 10;

    /// <summary>The record's own fields, the same for every company.</summary>
    public static readonly IReadOnlyList<ConditionField> BuiltInFields =
    [
        new(StepConditionRule.Source, "Talep kaynağı", ConditionFieldType.Choice, Enum.GetNames<RequestSource>()),
        new(StepConditionRule.RequestType, "Talep türü", ConditionFieldType.Choice, Enum.GetNames<RequestType>()),
        new(StepConditionRule.RequiresOffer, "Teklif hazırlanacak mı", ConditionFieldType.Boolean),
        new(StepConditionRule.Level, "Seviye", ConditionFieldType.Number),
    ];

    /// <summary>Every rule of a step; the first error of each rule. Empty when all can be saved.</summary>
    public static IReadOnlyList<StepRuleError> ValidateConditions(
        IReadOnlyList<StepConditionRule> rules, IEnumerable<ConditionField> customFields)
    {
        if (rules.Count > MaxRules)
        {
            return [new StepRuleError(MaxRules, $"Bir adımda en fazla {MaxRules} kural olabilir.")];
        }

        var fields = BuiltInFields.Concat(customFields).ToDictionary(f => f.Field, StringComparer.Ordinal);
        return rules
            .Select((rule, index) => ValidateCondition(rule, fields) is { } message ? new StepRuleError(index, message) : null)
            .OfType<StepRuleError>()
            .ToList();
    }

    /// <summary>Why the rule cannot be saved; null when it can.</summary>
    public static string? ValidateCondition(StepConditionRule rule, IReadOnlyDictionary<string, ConditionField> fields)
    {
        if (string.IsNullOrWhiteSpace(rule.Field))
        {
            return "Kural alanı seçilmelidir.";
        }

        if (!fields.TryGetValue(rule.Field, out var field))
        {
            return $"Bilinmeyen alan: «{rule.Field}».";
        }

        if (!Enum.IsDefined(rule.Op) || !Enum.IsDefined(rule.WhenEmpty))
        {
            return "Geçersiz atlama kuralı.";
        }

        if (rule.Op is ConditionOperator.AtLeast or ConditionOperator.AtMost
            && field.Type is not (ConditionFieldType.Number or ConditionFieldType.Date))
        {
            return $"«{field.Name}» için «en az» ve «en çok» kullanılamaz.";
        }

        if (rule.Op is ConditionOperator.IsSet or ConditionOperator.IsNotSet)
        {
            return rule.Value is null ? null : "«Dolu» ve «boş» karşılaştırmalarında değer girilmez.";
        }

        if (string.IsNullOrWhiteSpace(rule.Value))
        {
            return "Değer girilmelidir.";
        }

        if (rule.Value.Length > StepConditionRule.MaxValueLength)
        {
            return $"Değer en fazla {StepConditionRule.MaxValueLength} karakter olabilir.";
        }

        return field.Type switch
        {
            ConditionFieldType.Choice when field.Options is { } options
                && !options.Contains(rule.Value.Trim(), StringComparer.OrdinalIgnoreCase) =>
                $"«{rule.Value}», «{field.Name}» alanının seçeneklerinden biri değil.",
            ConditionFieldType.Boolean when rule.Value is not ("true" or "false") =>
                "Değer «Evet» ya da «Hayır» olmalıdır.",
            ConditionFieldType.Number when !decimal.TryParse(rule.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out _) =>
                "Değer bir sayı olmalıdır.",
            ConditionFieldType.Number when rule.Field == StepConditionRule.Level
                && rule.Value is not ("1" or "2" or "3") =>
                "Seviye 1 ile 3 arasında olmalıdır.",
            ConditionFieldType.Date when !DateOnly.TryParseExact(
                rule.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) =>
                "Tarih yyyy-aa-gg biçiminde olmalıdır.",
            _ => null,
        };
    }

    /// <summary>
    /// Every prerequisite of a step; the first error of each. Form and document type codes are the company's own;
    /// the same prerequisite twice is an error on the second one.
    /// </summary>
    public static IReadOnlyList<StepRuleError> ValidatePrerequisites(
        IReadOnlyList<StepPrerequisite> prerequisites,
        IReadOnlySet<string> questionnaireCodes,
        IReadOnlySet<string> documentTypeCodes)
    {
        if (prerequisites.Count > MaxPrerequisites)
        {
            return [new StepRuleError(MaxPrerequisites, $"Bir adımda en fazla {MaxPrerequisites} ön koşul olabilir.")];
        }

        var errors = new List<StepRuleError>();
        for (var index = 0; index < prerequisites.Count; index++)
        {
            var prerequisite = prerequisites[index];
            var message = prerequisites.Take(index).Contains(prerequisite)
                ? "Aynı ön koşul iki kez eklenemez."
                : ValidatePrerequisite(prerequisite, questionnaireCodes, documentTypeCodes);
            if (message is not null)
            {
                errors.Add(new StepRuleError(index, message));
            }
        }

        return errors;
    }

    /// <summary>Why the prerequisite cannot be saved; null when it can.</summary>
    public static string? ValidatePrerequisite(
        StepPrerequisite prerequisite, IReadOnlySet<string> questionnaireCodes, IReadOnlySet<string> documentTypeCodes)
    {
        if (!Enum.IsDefined(prerequisite.Kind))
        {
            return "Geçersiz ön koşul.";
        }

        return prerequisite.Kind switch
        {
            PrerequisiteKind.QuestionnaireComplete => ValidateCode(
                prerequisite.Value, questionnaireCodes, "Bu ön koşul için form seçilmelidir.", "form"),
            PrerequisiteKind.DocumentApproved => ValidateCode(
                prerequisite.Value, documentTypeCodes, "Bu ön koşul için doküman seçilmelidir.", "doküman türü"),
            _ => prerequisite.Value is null ? null : "Bu ön koşul bir değer almaz.",
        };
    }

    private static string? ValidateCode(string? value, IReadOnlySet<string> codes, string missing, string what) =>
        string.IsNullOrWhiteSpace(value) ? missing
        : value.Length > StepPrerequisite.MaxValueLength ? "Geçersiz ön koşul."
        : codes.Contains(value) ? null
        : $"Şirkette «{value}» kodlu bir {what} yok.";
}
