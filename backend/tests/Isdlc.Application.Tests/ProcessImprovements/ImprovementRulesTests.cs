using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.ProcessImprovements;
using Isdlc.Domain.ProcessImprovements;

namespace Isdlc.Application.Tests.ProcessImprovements;

/// <summary>Prioritization without a database. Contract: docs/process-improvement-api.md → Önceliklendirme.</summary>
public class ImprovementRulesTests
{
    [Theory]
    [InlineData(null, null, null)]
    [InlineData(1, 1, 1)]
    [InlineData(5, 5, 5)]
    public void CheckScores_AllOrNone_IsValid(int? impact, int? frequency, int? ease)
    {
        ImprovementRules.CheckScores(impact, frequency, ease);
    }

    [Theory]
    [InlineData(3, null, null)]
    [InlineData(null, 3, null)]
    [InlineData(null, null, 3)]
    [InlineData(3, 3, null)]
    [InlineData(3, null, 3)]
    [InlineData(null, 3, 3)]
    public void CheckScores_SomeMissing_Throws(int? impact, int? frequency, int? ease)
    {
        var ex = Assert.Throws<BusinessRuleException>(() => ImprovementRules.CheckScores(impact, frequency, ease));

        Assert.Equal(ImprovementRules.ScoresTogether, ex.Message);
    }

    [Fact]
    public void ScoreOf_IsTheSum_OrNullUntilAllThreeAreGiven()
    {
        Assert.Equal(13, ImprovementRules.ScoreOf(5, 4, 4));
        Assert.Null(ImprovementRules.ScoreOf(5, 4, null));
        Assert.Null(ImprovementRules.ScoreOf(null, null, null));
    }

    [Fact]
    public void PriorityOf_EveryScore_FallsInItsBand()
    {
        // Every sum three 1–5 scores can make.
        for (var score = 3; score <= 15; score++)
        {
            var expected = score >= 12 ? ImprovementPriority.High
                : score >= 8 ? ImprovementPriority.Medium
                : ImprovementPriority.Low;
            Assert.Equal(expected, ImprovementRules.PriorityOf(score));
        }
    }

    [Theory]
    [InlineData(7, ImprovementPriority.Low)]
    [InlineData(8, ImprovementPriority.Medium)]
    [InlineData(11, ImprovementPriority.Medium)]
    [InlineData(12, ImprovementPriority.High)]
    public void PriorityOf_BandEdges(int score, ImprovementPriority expected)
    {
        Assert.Equal(expected, ImprovementRules.PriorityOf(score));
    }

    [Fact]
    public void PriorityOf_NoScore_IsNull()
    {
        Assert.Null(ImprovementRules.PriorityOf(null));
    }
}
