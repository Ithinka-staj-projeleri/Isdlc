namespace Isdlc.Application.Audit;

/// <summary>Short titles for timeline rows, from longer texts. Pure, no database.</summary>
public static class TimelineText
{
    /// <summary>
    /// The first sentence of a step's text: "Talep alınır ve Proje No verilir. Talep alınır; ..." → "Talep alınır ve Proje No verilir".
    /// A text without a sentence end is returned whole.
    /// </summary>
    public static string FirstSentence(string text)
    {
        var trimmed = text.Trim();
        var end = trimmed.IndexOf(". ", StringComparison.Ordinal);
        return (end > 0 ? trimmed[..end] : trimmed).TrimEnd('.');
    }

    /// <summary>The first topic of a meeting's agenda, without its "- " mark; empty when there is none.</summary>
    public static string FirstAgendaLine(string agenda) =>
        agenda.Split('\n')
            .Select(line => line.Trim().TrimStart('-', '•', '*').Trim())
            .FirstOrDefault(line => line.Length > 0) ?? string.Empty;
}
