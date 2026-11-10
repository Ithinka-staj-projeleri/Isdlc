using System.Text.Json;
using Isdlc.Application.Assessment;
using Isdlc.Application.Assessment.Dtos;
using Isdlc.Domain.Assessment;

namespace Isdlc.Application.Tests.Assessment;

/// <summary>
/// Pure level and acceptance rules (no database), every combination. Expectations: docs/assessment-api.md.
/// The service's database tests are Erva's cross tests (PLANNING.md: Erva → Baran's modules).
/// </summary>
public class AssessmentRulesTests
{
    private static LevelAnswersDto Answers(int? effort, int? screens, int? integrations, int? roles, int? data, int? hardware) =>
        new()
        {
            Effort = effort, ScreensAndModules = screens, Integrations = integrations,
            UserRoles = roles, DataSensitivity = data, HardwareDependency = hardware,
        };

    private static AcceptanceScoresDto Scores(int? strategic, int? commercial, int? technical, int? resource, int? customer, int? risk) =>
        new()
        {
            StrategicFit = strategic, CommercialValue = commercial, TechnicalFeasibility = technical,
            ResourceAndSchedule = resource, CustomerFit = customer, RiskLevel = risk,
        };

    // ---- Definition: one source for the screen and the API ----

    [Fact]
    public void Definition_LevelQuestionKeys_AreTheJsonNamesOfTheAnswersInOrder()
    {
        // The screen sends answers under these keys; a renamed property must not silently stop matching.
        var jsonNames = typeof(LevelAnswersDto).GetProperties()
            .Select(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name));

        Assert.Equal(jsonNames, AssessmentRules.LevelQuestions.Select(q => q.Key));
    }

    [Fact]
    public void Definition_AcceptanceCriterionKeys_AreTheJsonNamesOfTheScoresInOrder()
    {
        var jsonNames = typeof(AcceptanceScoresDto).GetProperties()
            .Select(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name));

        Assert.Equal(jsonNames, AssessmentRules.AcceptanceCriteria.Select(c => c.Key));
    }

    [Fact]
    public void Definition_EveryLevelQuestion_HasOptions1To3()
    {
        Assert.Equal(6, AssessmentRules.LevelQuestions.Count);
        Assert.All(AssessmentRules.LevelQuestions, q => Assert.Equal([1, 2, 3], q.Options.Select(o => o.Value)));
    }

    [Fact]
    public void Definition_AcceptanceWeights_AddUpTo100()
    {
        Assert.Equal(6, AssessmentRules.AcceptanceCriteria.Count);
        Assert.Equal(100, AssessmentRules.AcceptanceCriteria.Sum(c => c.Weight));
    }

    // ---- Level ----

    [Fact]
    public void SuggestedLevel_EveryCombination_FollowsTheRule()
    {
        int[] values = [1, 2, 3];
        var count = 0;
        foreach (var e in values) foreach (var s in values) foreach (var i in values)
        foreach (var r in values) foreach (var d in values) foreach (var h in values)
        {
            var answers = Answers(e, s, i, r, d, h);
            var score = e + s + i + r + d + h;
            var expected = e == 3 || score >= 14 ? 3 : score >= 9 ? 2 : 1;

            Assert.Equal(score, AssessmentRules.LevelScore(answers));
            Assert.Equal(expected, AssessmentRules.SuggestedLevel(answers));
            count++;
        }

        Assert.Equal(729, count);
    }

    [Theory]
    [InlineData(1, 1, 1, 1, 1, 1, 6, 1)]   // lowest score
    [InlineData(2, 2, 1, 1, 1, 1, 8, 1)]   // just below level 2
    [InlineData(2, 2, 2, 1, 1, 1, 9, 2)]   // level 2 starts at 9
    [InlineData(2, 2, 2, 3, 3, 1, 13, 2)]  // highest level 2
    [InlineData(2, 2, 3, 3, 3, 1, 14, 3)]  // level 3 starts at 14
    [InlineData(3, 1, 1, 1, 1, 1, 8, 3)]   // effort above 80 person-days is level 3 on its own
    [InlineData(3, 3, 3, 3, 3, 3, 18, 3)]  // highest score
    public void SuggestedLevel_Thresholds(int e, int s, int i, int r, int d, int h, int score, int level)
    {
        var answers = Answers(e, s, i, r, d, h);

        Assert.Equal(score, AssessmentRules.LevelScore(answers));
        Assert.Equal(level, AssessmentRules.SuggestedLevel(answers));
    }

    [Fact]
    public void SuggestedLevel_OneQuestionUnanswered_IsNull()
    {
        var answers = Answers(3, 3, 3, 3, 3, null);

        Assert.Null(AssessmentRules.LevelScore(answers));
        Assert.Null(AssessmentRules.SuggestedLevel(answers));
        Assert.Equal(5, AssessmentRules.AnsweredCount(answers));
    }

    [Fact]
    public void AnsweredCount_NothingAnswered_IsZero()
    {
        Assert.Equal(0, AssessmentRules.AnsweredCount(new LevelAnswersDto()));
        Assert.Equal(0, AssessmentRules.AnsweredCount(new AcceptanceScoresDto()));
    }

    // ---- Acceptance ----

    [Theory]
    [InlineData(4, 4, 3, 3, 5, 3, 3.70)]  // the contract's example
    [InlineData(1, 1, 1, 1, 1, 1, 1.00)]
    [InlineData(5, 5, 5, 5, 5, 5, 5.00)]
    [InlineData(5, 1, 1, 1, 1, 1, 1.80)]  // 20% weight
    [InlineData(1, 1, 1, 5, 1, 1, 1.60)]  // 15% weight
    [InlineData(1, 1, 1, 1, 1, 5, 1.40)]  // 10% weight
    public void WeightedScore_IsTheWeightedAverage(int a, int b, int c, int d, int e, int f, double expected)
    {
        Assert.Equal((decimal)expected, AssessmentRules.WeightedScore(Scores(a, b, c, d, e, f)));
    }

    [Fact]
    public void WeightedScore_EveryCombination_IsBetween1And5WithTwoDecimals()
    {
        int[] values = [1, 2, 3, 4, 5];
        foreach (var a in values) foreach (var b in values) foreach (var c in values)
        foreach (var d in values) foreach (var e in values) foreach (var f in values)
        {
            var score = AssessmentRules.WeightedScore(Scores(a, b, c, d, e, f))!.Value;

            Assert.InRange(score, 1.00m, 5.00m);
            Assert.Equal(score, Math.Round(score, 2));
        }
    }

    [Fact]
    public void WeightedScore_OneCriterionUnscored_IsNull()
    {
        var scores = Scores(5, 5, 5, 5, 5, null);

        Assert.Null(AssessmentRules.WeightedScore(scores));
        Assert.Equal(5, AssessmentRules.AnsweredCount(scores));
    }

    [Theory]
    [InlineData(5.00, AcceptanceRecommendation.Accept)]
    [InlineData(3.50, AcceptanceRecommendation.Accept)]
    [InlineData(3.49, AcceptanceRecommendation.Conditional)]
    [InlineData(2.50, AcceptanceRecommendation.Conditional)]
    [InlineData(2.49, AcceptanceRecommendation.RejectOrPostpone)]
    [InlineData(1.00, AcceptanceRecommendation.RejectOrPostpone)]
    public void RecommendationOf_Thresholds(double weightedScore, AcceptanceRecommendation expected)
    {
        Assert.Equal(expected, AssessmentRules.RecommendationOf((decimal)weightedScore));
    }
}
