using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Isdlc.Domain.Process;

/// <summary>
/// One rule of a step's skip condition (Faz 7 · A5): a field of the record, a comparison and a value. The step is done on
/// a record when all its rules hold. Kept as a JSON list on the step (ProcessStep.Conditions).
/// Rules: docs/configurability.md → Atlama kuralları.
/// </summary>
/// <param name="Field">"source", "requestType", "requiresOffer", "level" or "custom:{key}".</param>
/// <param name="Value">Compared as text; numbers and true / false are written to JSON as such. Empty for IsSet / IsNotSet.</param>
/// <param name="WhenEmpty">The rule's result while the record's field is empty.</param>
public record StepConditionRule(
    string Field,
    ConditionOperator Op,
    [property: JsonConverter(typeof(ConditionValueJsonConverter))] string? Value = null,
    ConditionWhenEmpty WhenEmpty = ConditionWhenEmpty.NotMet)
{
    public const string Source = "source";
    public const string RequestType = "requestType";
    public const string RequiresOffer = "requiresOffer";
    public const string Level = "level";
    public const string CustomPrefix = "custom:";

    public const int MaxFieldLength = 60;
    public const int MaxValueLength = 100;
}

public enum ConditionOperator
{
    Equals,
    NotEquals,
    AtLeast,
    AtMost,
    IsSet,
    IsNotSet,
}

public enum ConditionWhenEmpty
{
    NotMet,
    Met,
}

/// <summary>
/// Reads a JSON string, number or true / false as text and writes it back the same way: "External" stays a string,
/// 2 and true are written without quotes, so the seed files and the column read naturally.
/// </summary>
public sealed class ConditionValueJsonConverter : JsonConverter<string?>
{
    public override bool HandleNull => true;

    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.True => "true",
            JsonTokenType.False => "false",
            JsonTokenType.Number => reader.GetDecimal().ToString(CultureInfo.InvariantCulture),
            _ => throw new JsonException("A condition value must be text, a number or true / false."),
        };

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else if (value is "true" or "false")
        {
            writer.WriteBooleanValue(value == "true");
        }
        else if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
                 && number.ToString(CultureInfo.InvariantCulture) == value)
        {
            writer.WriteNumberValue(number);
        }
        else
        {
            writer.WriteStringValue(value);
        }
    }
}
