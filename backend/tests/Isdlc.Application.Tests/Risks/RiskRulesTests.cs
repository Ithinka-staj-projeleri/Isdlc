using Isdlc.Application.Risks;
using Isdlc.Domain.Risks;

namespace Isdlc.Application.Tests.Risks;

/// <summary>
/// Pure risk rules (no database), every combination. Expectations: docs/risks-api.md → "Skor ve seviye".
/// The service's database tests are Erva's cross tests (PLANNING.md: Erva → Baran's modules).
/// </summary>
public class RiskRulesTests
{
    // ---- Score ----

    [Fact]
    public void Score_EveryCombination_IsProbabilityTimesImpactBetween1And25()
    {
        for (var probability = 1; probability <= 5; probability++)
        {
            for (var impact = 1; impact <= 5; impact++)
            {
                var score = RiskRules.Score(probability, impact);

                Assert.Equal(probability * impact, score);
                Assert.InRange(score, 1, 25);
            }
        }
    }

    // ---- SeverityOf: 1–7 Low, 8–14 Medium, 15–25 High ----

    [Theory]
    [InlineData(1, RiskSeverity.Low)]
    [InlineData(7, RiskSeverity.Low)]
    [InlineData(8, RiskSeverity.Medium)]
    [InlineData(14, RiskSeverity.Medium)]
    [InlineData(15, RiskSeverity.High)]
    [InlineData(25, RiskSeverity.High)]
    public void SeverityOf_Thresholds_MatchTheHeatMap(int score, RiskSeverity expected)
    {
        Assert.Equal(expected, RiskRules.SeverityOf(score));
    }

    [Fact]
    public void SeverityOf_EveryPossibleScore_FallsInsideItsScoreRange()
    {
        // The list filter uses ScoreRange in the database; it must agree with SeverityOf for every real score.
        for (var probability = 1; probability <= 5; probability++)
        {
            for (var impact = 1; impact <= 5; impact++)
            {
                var score = RiskRules.Score(probability, impact);
                var (min, max) = RiskRules.ScoreRange(RiskRules.SeverityOf(score));

                Assert.InRange(score, min, max);
            }
        }
    }

    // ---- ScoreRange ----

    [Theory]
    [InlineData(RiskSeverity.Low, 1, 7)]
    [InlineData(RiskSeverity.Medium, 8, 14)]
    [InlineData(RiskSeverity.High, 15, 25)]
    public void ScoreRange_EachSeverity_CoversItsScoresWithoutGaps(RiskSeverity severity, int min, int max)
    {
        Assert.Equal((min, max), RiskRules.ScoreRange(severity));
    }

    // ---- BecameHigh: who should hear that a risk is now high ----

    [Fact]
    public void BecameHigh_NewHighRisk_ReturnsTrue()
    {
        Assert.True(RiskRules.BecameHigh(previousScore: null, previousStatus: null, score: 16, RiskStatus.Open));
    }

    [Fact]
    public void BecameHigh_NewRiskThatIsNotHigh_ReturnsFalse()
    {
        Assert.False(RiskRules.BecameHigh(previousScore: null, previousStatus: null, score: 14, RiskStatus.Open));
    }

    [Fact]
    public void BecameHigh_MediumRaisedToHigh_ReturnsTrue()
    {
        Assert.True(RiskRules.BecameHigh(previousScore: 12, RiskStatus.Open, score: 20, RiskStatus.Monitoring));
    }

    [Fact]
    public void BecameHigh_AlreadyHighAndStillHigh_ReturnsFalse()
    {
        // Changing a high risk from 16 to 20 is not news: the manager was told when it first became high.
        Assert.False(RiskRules.BecameHigh(previousScore: 16, RiskStatus.Open, score: 20, RiskStatus.Open));
    }

    [Fact]
    public void BecameHigh_HighRiskClosed_ReturnsFalse()
    {
        Assert.False(RiskRules.BecameHigh(previousScore: 16, RiskStatus.Open, score: 16, RiskStatus.Closed));
    }

    [Fact]
    public void BecameHigh_ClosedHighRiskReopened_ReturnsTrue()
    {
        // A closed risk does not count; reopening a high one makes it high again.
        Assert.True(RiskRules.BecameHigh(previousScore: 16, RiskStatus.Closed, score: 16, RiskStatus.Open));
    }

    [Fact]
    public void BecameHigh_HighRiskLowered_ReturnsFalse()
    {
        Assert.False(RiskRules.BecameHigh(previousScore: 20, RiskStatus.Open, score: 9, RiskStatus.Open));
    }
}
