using System.Text.Json;
using Isdlc.Application.QuestionBank.Dtos;

namespace Isdlc.Application.Assessment;

/// <summary>One range of a form's thresholds: "21–40 → 2".</summary>
public record ScoreRange(int Min, int Max, string Result);

/// <summary>The thresholds of one section of a form (QuestionSet.Bands); Section is null for a form without sections.</summary>
public record ScoreBand(string? Section, string Title, IReadOnlyList<ScoreRange> Ranges);

/// <summary>
/// Reads QuestionSet.Bands and adds up the points of a form's answers. Pure: the thresholds come from the seed file,
/// never from code (docs/assessment-api.md → Sonuçlar).
/// </summary>
public static class ScoreBands
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static IReadOnlyList<ScoreBand> Parse(string? json) =>
        string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<List<ScoreBand>>(json, JsonOptions) ?? [];

    /// <summary>The band of a section; an error in the seed file (a missing band) is a programming error.</summary>
    public static ScoreBand Of(IReadOnlyList<ScoreBand> bands, string? section) =>
        bands.SingleOrDefault(b => b.Section == section)
        ?? throw new InvalidOperationException($"The form has no score band for section '{section ?? "(none)"}'.");

    /// <summary>
    /// The range the score falls into. Some options are 0 points, so a score below the first range takes the first
    /// range's result (decision: 0 → Seviye 1, 1 gün); a score above the last takes the last.
    /// </summary>
    public static string ResultOf(ScoreBand band, int score)
    {
        var ranges = band.Ranges.OrderBy(r => r.Min).ToList();
        return ranges.FirstOrDefault(r => score >= r.Min && score <= r.Max)?.Result
            ?? (score < ranges[0].Min ? ranges[0] : ranges[^1]).Result;
    }

    /// <summary>Points of the chosen options, the highest possible points, answered and listed questions.</summary>
    public static (int Score, int MaxScore, int Answered, int Listed) Total(IEnumerable<QuestionnaireItemDto> items)
    {
        int score = 0, maxScore = 0, answered = 0, listed = 0;
        foreach (var item in items)
        {
            listed++;
            maxScore += item.Question.Options.Select(o => o.Points ?? 0).DefaultIfEmpty().Max();

            var chosen = item.Answer?.OptionId is { } optionId
                ? item.Question.Options.FirstOrDefault(o => o.Id == optionId)
                : null;
            if (chosen is not null)
            {
                answered++;
                score += chosen.Points ?? 0;
            }
        }

        return (score, maxScore, answered, listed);
    }
}
