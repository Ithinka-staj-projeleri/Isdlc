using Isdlc.Domain.QuestionBank;

namespace Isdlc.Application.QuestionBank;

/// <summary>
/// Talep Analizi (Talep Anlama Soru Listesi): which answers are open items and what the open issue made from one says.
/// Pure rules with no database access. Contract: docs/question-bank-api.md → "Talep Analizi (E3)".
/// </summary>
public static class RequestAnalysisRules
{
    public const string SetCode = "request-analysis";

    public const int MaxTitleLength = 300;
    public const int MaxDescriptionLength = 2000;

    /// <summary>"Açık Konu Var" and "Kısmen Yanıtlandı" answers still need something from the requester.</summary>
    public static bool IsOpenItem(OptionMeaning? meaning) => meaning is OptionMeaning.Open or OptionMeaning.Partial;

    /// <summary>The question itself, shortened with "…" when it is longer than an open issue's title.</summary>
    public static string Title(string questionText)
    {
        var text = questionText.Trim();
        return text.Length <= MaxTitleLength ? text : text[..(MaxTitleLength - 1)].TrimEnd() + "…";
    }

    /// <summary>
    /// Where the record came from and what is missing: the answer's status, the open issue / missing information and the
    /// stakeholder's answer, one per line; empty parts are left out. Shortened with "…" to fit the field.
    /// </summary>
    public static string Description(int questionNo, string? group, string optionLabel, string? note, string? answerText)
    {
        var lines = new List<string>
        {
            $"Talep Analizi · soru {questionNo}{(string.IsNullOrWhiteSpace(group) ? "" : $" ({group.Trim()})")}: {optionLabel}",
        };
        if (!string.IsNullOrWhiteSpace(note))
        {
            lines.Add($"Açık konu / eksik bilgi: {note.Trim()}");
        }
        if (!string.IsNullOrWhiteSpace(answerText))
        {
            lines.Add($"Paydaş yanıtı: {answerText.Trim()}");
        }

        var description = string.Join("\n", lines);
        return description.Length <= MaxDescriptionLength
            ? description
            : description[..(MaxDescriptionLength - 1)].TrimEnd() + "…";
    }
}
