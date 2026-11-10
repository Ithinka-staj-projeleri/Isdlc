using System.Globalization;
using System.Text.Json;

namespace Isdlc.Application.Audit;

/// <summary>
/// Reads AuditLog.Changes ({"Status": {"old": "Open", "new": "Closed"}}) into fields with text values.
/// Pure, no database: references (user ids, step ids...) are resolved afterwards by AuditService.
/// </summary>
public static class AuditChangeReader
{
    /// <summary>Fields that point to another record; their ids are turned into names or codes.</summary>
    public static readonly IReadOnlySet<string> UserFields =
        new HashSet<string> { "OwnerUserId", "BusinessAnalystId", "ProjectManagerId" };

    public const string StepField = "CurrentStepId";
    public const string MeetingField = "SourceMeetingId";
    public const string DocumentTypeField = "DocumentTypeId";

    /// <summary>Same on every row of a project's log, so it is left out.</summary>
    private const string ProjectField = "ProjectId";

    public record Field(string Name, string? Old, string? New);

    /// <summary>Fields in the stored order. Broken JSON gives an empty list instead of an error (the log is history).</summary>
    public static IReadOnlyList<Field> Read(string changesJson)
    {
        try
        {
            using var document = JsonDocument.Parse(changesJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            return document.RootElement.EnumerateObject()
                .Where(p => p.Name != ProjectField)
                .Select(p => new Field(p.Name, ValueOf(p.Value, "old"), ValueOf(p.Value, "new")))
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>The id in a reference field's value, or null when the value is empty or not a number.</summary>
    public static int? IdOf(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : null;

    private static string? ValueOf(JsonElement change, string key)
    {
        if (change.ValueKind != JsonValueKind.Object || !change.TryGetProperty(key, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.String => value.GetString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => value.GetRawText(), // numbers keep their invariant form: "3", "12.5"
        };
    }
}
