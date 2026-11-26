using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Isdlc.Domain.Projects;

namespace Isdlc.Application.Projects;

/// <summary>A company field as the value rules need it.</summary>
public record CustomFieldDefinition(string Key, CustomFieldType Type, IReadOnlyList<string> Options, bool IsRequired);

/// <summary>The record's new values (JSON, null when none) or the errors by JSON path ("customValues.butce").</summary>
public record CustomValuesResult(string? Json, IReadOnlyDictionary<string, string[]> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

/// <summary>
/// Pure rules for the values of the company's own fields on a record (Faz 7 · A6): reading, checking and writing
/// Project.CustomValues. No database access, so every type and edge case is unit tested.
/// Rules: docs/configurability.md → Şirkete özel alanlar.
/// </summary>
public static class CustomValueRules
{
    public const string ErrorKeyPrefix = "customValues.";
    public const string RequiredMessage = "Bu alan zorunludur.";
    public const string TextTooLongMessage = "En fazla 500 karakter olabilir.";
    public const string TextMessage = "Metin girilmelidir.";
    public const string NumberMessage = "Bir sayı girilmelidir.";
    public const string DateMessage = "Geçerli bir tarih girilmelidir.";
    public const string SelectMessage = "Seçeneklerden biri seçilmelidir.";
    public const string BooleanMessage = "Evet ya da Hayır seçilmelidir.";

    private const string DateFormat = "yyyy-MM-dd";

    private static readonly JsonWriterOptions WriterOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>The stored values by key, as the API returns them. Empty for null or unreadable JSON.</summary>
    public static IReadOnlyDictionary<string, JsonElement> Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, JsonElement>();
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? document.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone())
                : new Dictionary<string, JsonElement>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, JsonElement>();
        }
    }

    /// <summary>
    /// The stored values as text, for the skip rules (StepConditions): numbers in invariant culture, true / false,
    /// dates as yyyy-MM-dd, the chosen option.
    /// </summary>
    public static IReadOnlyDictionary<string, string> ToText(string? json) =>
        Read(json)
            .Select(pair => (pair.Key, Text: TextOf(pair.Value)))
            .Where(pair => pair.Text is not null)
            .ToDictionary(pair => pair.Key, pair => pair.Text!);

    /// <summary>
    /// Checks the values sent for the company's active fields and returns the record's new JSON.
    /// <list type="bullet">
    /// <item>A required field that is missing, null or blank is an error; an empty optional field is left out.</item>
    /// <item>Each value must match its field's type; the chosen option is kept as the field spells it.</item>
    /// <item>Keys that are not active fields are ignored. Values of fields that are not active any more stay as they were
    /// (an inactive field is hidden from the form, its value is kept).</item>
    /// </list>
    /// </summary>
    public static CustomValuesResult Apply(
        IReadOnlyList<CustomFieldDefinition> activeFields,
        string? storedJson,
        IReadOnlyDictionary<string, JsonElement>? input)
    {
        var errors = new Dictionary<string, string[]>();
        var values = new List<KeyValuePair<string, JsonElement>>();
        var activeKeys = activeFields.Select(f => f.Key).ToHashSet(StringComparer.Ordinal);

        foreach (var field in activeFields)
        {
            JsonElement? sent = input is not null && input.TryGetValue(field.Key, out var element) ? element : null;
            var (value, error) = Normalize(field, sent);

            if (error is not null)
            {
                errors[ErrorKeyPrefix + field.Key] = [error];
            }
            else if (value is not null)
            {
                values.Add(new(field.Key, value.Value));
            }
            else if (field.IsRequired)
            {
                errors[ErrorKeyPrefix + field.Key] = [RequiredMessage];
            }
        }

        if (errors.Count > 0)
        {
            return new CustomValuesResult(storedJson, errors);
        }

        values.AddRange(Read(storedJson).Where(pair => !activeKeys.Contains(pair.Key)));

        return new CustomValuesResult(values.Count == 0 ? null : Write(values), errors);
    }

    /// <summary>Whether every required active field has a value: part of the "Talep Kayıt Formu dolu" prerequisite.</summary>
    public static bool RequiredFieldsFilled(IEnumerable<CustomFieldDefinition> activeFields, string? storedJson)
    {
        var stored = Read(storedJson);
        return activeFields
            .Where(f => f.IsRequired)
            .All(f => stored.TryGetValue(f.Key, out var value) && Normalize(f, value) is { Value: not null, Error: null });
    }

    /// <summary>The value to keep (null: empty) or the error to show.</summary>
    private static (JsonElement? Value, string? Error) Normalize(CustomFieldDefinition field, JsonElement? sent)
    {
        if (sent is not { } value
            || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
            || (value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString())))
        {
            return (null, null);
        }

        return field.Type switch
        {
            CustomFieldType.Text => NormalizeText(value),

            CustomFieldType.Number => value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out _)
                ? (value.Clone(), null)
                : (null, NumberMessage),

            CustomFieldType.Date => value.ValueKind == JsonValueKind.String
                && DateOnly.TryParseExact(value.GetString()!.Trim(), DateFormat, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var date)
                ? (StringElement(date.ToString(DateFormat, CultureInfo.InvariantCulture)), null)
                : (null, DateMessage),

            CustomFieldType.Select => value.ValueKind == JsonValueKind.String
                && field.Options.FirstOrDefault(o =>
                    string.Equals(o, value.GetString()!.Trim(), StringComparison.OrdinalIgnoreCase)) is { } option
                ? (StringElement(option), null)
                : (null, SelectMessage),

            CustomFieldType.Boolean => value.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? (value.Clone(), null)
                : (null, BooleanMessage),

            _ => (null, TextMessage),
        };
    }

    private static (JsonElement? Value, string? Error) NormalizeText(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            return (null, TextMessage);
        }

        var text = value.GetString()!.Trim();
        return text.Length > CustomField.MaxTextValueLength ? (null, TextTooLongMessage) : (StringElement(text), null);
    }

    private static string? TextOf(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number => value.TryGetDecimal(out var number)
            ? number.ToString(CultureInfo.InvariantCulture)
            : value.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => null,
    };

    private static JsonElement StringElement(string text) => JsonSerializer.SerializeToElement(text);

    private static string Write(IEnumerable<KeyValuePair<string, JsonElement>> values)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            writer.WriteStartObject();
            foreach (var (key, value) in values)
            {
                writer.WritePropertyName(key);
                value.WriteTo(writer);
            }
            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }
}
