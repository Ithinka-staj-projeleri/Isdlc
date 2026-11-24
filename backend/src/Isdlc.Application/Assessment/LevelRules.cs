using System.Globalization;
using Isdlc.Application.Assessment.Dtos;
using Isdlc.Application.QuestionBank.Dtos;

namespace Isdlc.Application.Assessment;

/// <summary>
/// Seviye Belirleme Şablonu (008): section 1 gives the document level (1–3), section 2 the research days.
/// Pure: no database, every case unit tested. Rules: docs/assessment-api.md → "Seviye".
/// </summary>
public static class LevelRules
{
    public const string SetCode = "level";
    public const string DocumentLevelSection = "1";
    public const string ResearchSection = "2";

    public static LevelResultDto Calculate(
        IReadOnlyList<QuestionnaireItemDto> items, IReadOnlyList<ScoreBand> bands, int? appliedLevel, int? appliedResearchDays)
    {
        var document = Section(items, bands, DocumentLevelSection);
        var research = Section(items, bands, ResearchSection);

        return new LevelResultDto(
            new DocumentLevelResultDto(document.Score, document.MaxScore, document.Answered, document.Listed,
                document.Result is { } level ? int.Parse(level, CultureInfo.InvariantCulture) : null),
            new ResearchResultDto(research.Score, research.MaxScore, research.Answered, research.Listed,
                research.Result is { } days ? DaysOf(days) : null),
            appliedLevel,
            appliedResearchDays);
    }

    /// <summary>Both sections are complete, so the level and the research days can be applied.</summary>
    public static bool CanApply(LevelResultDto result) => result.DocumentLevel.Level is not null && result.Research.Days is not null;

    /// <summary>"5d" in the seed file → 5 days.</summary>
    public static int DaysOf(string result) =>
        int.Parse(result.EndsWith('d') ? result[..^1] : result, CultureInfo.InvariantCulture);

    /// <summary>The section's total; its result only when every listed question of the section is answered.</summary>
    private static (int Score, int MaxScore, int Answered, int Listed, string? Result) Section(
        IReadOnlyList<QuestionnaireItemDto> items, IReadOnlyList<ScoreBand> bands, string section)
    {
        var (score, maxScore, answered, listed) = ScoreBands.Total(items.Where(i => i.Question.Section == section));
        var result = listed > 0 && answered == listed ? ScoreBands.ResultOf(ScoreBands.Of(bands, section), score) : null;
        return (score, maxScore, answered, listed, result);
    }
}
