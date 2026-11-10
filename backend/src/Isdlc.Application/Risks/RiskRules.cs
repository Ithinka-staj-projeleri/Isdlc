using Isdlc.Domain.Risks;

namespace Isdlc.Application.Risks;

/// <summary>
/// Pure rules for risks: score and severity. No database access, so they can be unit tested directly.
/// Rules: docs/risks-api.md → "Skor ve seviye".
/// </summary>
public static class RiskRules
{
    public const string CodePrefix = "RSK";

    public const int MinLevel = 1;
    public const int MaxLevel = 5;

    /// <summary>Highest score of each severity; the thresholds of the prototype's heat map.</summary>
    public const int LowMaxScore = 7;
    public const int MediumMaxScore = 14;

    /// <summary>Probability × impact, 1–25.</summary>
    public static int Score(int probability, int impact) => probability * impact;

    /// <summary>1–7 Low, 8–14 Medium, 15–25 High.</summary>
    public static RiskSeverity SeverityOf(int score) => score switch
    {
        <= LowMaxScore => RiskSeverity.Low,
        <= MediumMaxScore => RiskSeverity.Medium,
        _ => RiskSeverity.High,
    };

    /// <summary>Lowest and highest score of a severity, for filtering in the database.</summary>
    public static (int Min, int Max) ScoreRange(RiskSeverity severity) => severity switch
    {
        RiskSeverity.Low => (1, LowMaxScore),
        RiskSeverity.Medium => (LowMaxScore + 1, MediumMaxScore),
        _ => (MediumMaxScore + 1, MaxLevel * MaxLevel),
    };

    /// <summary>
    /// The risk has just become high: open and high now, and not high before (previousScore null for a new risk).
    /// A closed risk never counts as just becoming high.
    /// </summary>
    public static bool BecameHigh(int? previousScore, RiskStatus? previousStatus, int score, RiskStatus status)
    {
        if (status == RiskStatus.Closed || SeverityOf(score) != RiskSeverity.High)
        {
            return false;
        }

        var wasHigh = previousScore is { } before
            && previousStatus != RiskStatus.Closed
            && SeverityOf(before) == RiskSeverity.High;
        return !wasHigh;
    }
}
