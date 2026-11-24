using Isdlc.Application.Assessment;
using Isdlc.Application.Assessment.Dtos;
using Isdlc.Application.QuestionBank.Dtos;
using Isdlc.Domain.Assessment;

namespace Isdlc.Application.Tests.Assessment;

/// <summary>
/// Seviye and uygunluk rules without a database: every band boundary of the forms.
/// The bands are the ones in the seed files (docs/assessment-api.md → Sonuçlar).
/// </summary>
public class LevelRulesTests
{
    private static readonly IReadOnlyList<ScoreBand> LevelBands = ScoreBands.Parse("""
        [
          { "section": "1", "title": "Doküman Seviyesi", "ranges": [
            { "min": 1, "max": 20, "result": "1" }, { "min": 21, "max": 40, "result": "2" }, { "min": 41, "max": 60, "result": "3" } ] },
          { "section": "2", "title": "Araştırma Seviyesi", "ranges": [
            { "min": 1, "max": 10, "result": "1d" }, { "min": 11, "max": 20, "result": "2d" }, { "min": 21, "max": 30, "result": "5d" },
            { "min": 31, "max": 40, "result": "10d" }, { "min": 41, "max": 50, "result": "20d" }, { "min": 51, "max": 60, "result": "30d" } ] }
        ]
        """);

    private static readonly IReadOnlyList<ScoreBand> FitBands = ScoreBands.Parse("""
        [ { "section": null, "title": "Müşteri Uygunluğu", "ranges": [
            { "min": 0, "max": 49, "result": "NotFit" }, { "min": 50, "max": 69, "result": "Risky" },
            { "min": 70, "max": 84, "result": "ConditionallyFit" }, { "min": 85, "max": 100, "result": "Fit" } ] } ]
        """);

    private static int _nextId = 1;

    /// <summary>A question with options worth 0–maxPoints; answered with the option worth chosen (null: not answered).</summary>
    private static QuestionnaireItemDto Item(string? section, int? chosen, int maxPoints = 5)
    {
        var options = Enumerable.Range(0, maxPoints + 1)
            .Select(points => new QuestionOptionDto(_nextId++, $"o{points}", points.ToString(), points, null, false, false))
            .ToList();
        var question = new QuestionDto(_nextId++, "q", 1, section, null, null, "Soru?", null, null, null, null, null, options);
        var answer = chosen is { } points
            ? new AnswerDto(options.Single(o => o.Points == points).Id, null, null, null, null, null, null)
            : null;
        return new QuestionnaireItemDto(question, answer);
    }

    /// <summary>Questions of one section answered with the given points.</summary>
    private static IEnumerable<QuestionnaireItemDto> Section(string section, params int?[] chosen) =>
        chosen.Select(points => Item(section, points));

    // ---- Bands ----

    [Theory]
    [InlineData(0, "1")]   // below the first range: the first range's result
    [InlineData(1, "1")]
    [InlineData(20, "1")]
    [InlineData(21, "2")]
    [InlineData(40, "2")]
    [InlineData(41, "3")]
    [InlineData(60, "3")]
    [InlineData(61, "3")]  // above the last range: the last range's result
    public void ResultOf_DocumentLevelBoundaries(int score, string expected)
    {
        Assert.Equal(expected, ScoreBands.ResultOf(ScoreBands.Of(LevelBands, "1"), score));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(10, 1)]
    [InlineData(11, 2)]
    [InlineData(21, 5)]
    [InlineData(31, 10)]
    [InlineData(41, 20)]
    [InlineData(50, 20)]
    [InlineData(51, 30)]
    [InlineData(60, 30)]
    public void ResultOf_ResearchBoundaries_GiveDays(int score, int expectedDays)
    {
        Assert.Equal(expectedDays, LevelRules.DaysOf(ScoreBands.ResultOf(ScoreBands.Of(LevelBands, "2"), score)));
    }

    [Fact]
    public void Of_MissingSection_IsAProgrammingError()
    {
        Assert.Throws<InvalidOperationException>(() => ScoreBands.Of(LevelBands, "3"));
    }

    [Fact]
    public void Parse_NoBands_IsEmpty()
    {
        Assert.Empty(ScoreBands.Parse(null));
        Assert.Empty(ScoreBands.Parse(" "));
    }

    // ---- Level form ----

    [Fact]
    public void Calculate_BothSectionsAnswered_GivesLevelAndDays()
    {
        // Section 1: 5 + 5 + 4 + 4 + 5 = 23 → Seviye 2. Section 2: 3 + 4 + 5 = 12 → 2 gün.
        var items = Section("1", 5, 5, 4, 4, 5).Concat(Section("2", 3, 4, 5)).ToList();

        var result = LevelRules.Calculate(items, LevelBands, appliedLevel: 1, appliedResearchDays: null);

        Assert.Equal(new DocumentLevelResultDto(23, 25, 5, 5, 2), result.DocumentLevel);
        Assert.Equal(new ResearchResultDto(12, 15, 3, 3, 2), result.Research);
        Assert.Equal((1, (int?)null), (result.AppliedLevel, result.AppliedResearchDays));
        Assert.True(LevelRules.CanApply(result));
    }

    [Fact]
    public void Calculate_SectionPartlyAnswered_ShowsScoreButNoResult()
    {
        var items = Section("1", 5, null, 5).Concat(Section("2", 1, 1)).ToList();

        var result = LevelRules.Calculate(items, LevelBands, null, null);

        Assert.Equal(new DocumentLevelResultDto(10, 15, 2, 3, null), result.DocumentLevel);
        Assert.Equal(1, result.Research.Days);
        Assert.False(LevelRules.CanApply(result));
    }

    [Fact]
    public void Calculate_AllZeroPoints_IsLevelOneAndOneDay()
    {
        var items = Section("1", 0, 0).Concat(Section("2", 0, 0)).ToList();

        var result = LevelRules.Calculate(items, LevelBands, null, null);

        Assert.Equal((1, 1), (result.DocumentLevel.Level, result.Research.Days));
    }

    [Fact]
    public void Calculate_NoQuestionsListed_HasNoResult()
    {
        var result = LevelRules.Calculate([], LevelBands, null, null);

        Assert.Equal(new DocumentLevelResultDto(0, 0, 0, 0, null), result.DocumentLevel);
        Assert.Null(result.Research.Days);
        Assert.False(LevelRules.CanApply(result));
    }

    // ---- Customer fit survey ----

    [Theory]
    [InlineData(0, CustomerFitRating.NotFit)]
    [InlineData(49, CustomerFitRating.NotFit)]
    [InlineData(50, CustomerFitRating.Risky)]
    [InlineData(69, CustomerFitRating.Risky)]
    [InlineData(70, CustomerFitRating.ConditionallyFit)]
    [InlineData(84, CustomerFitRating.ConditionallyFit)]
    [InlineData(85, CustomerFitRating.Fit)]
    [InlineData(100, CustomerFitRating.Fit)]
    public void RatingOf_Boundaries(int score, CustomerFitRating expected)
    {
        Assert.Equal(expected, CustomerFitRules.RatingOf(FitBands, score));
    }

    [Fact]
    public void CustomerFit_AllAnswered_GivesTheRating()
    {
        var items = new int?[] { 10, 9, 8, 7, 9, 8, 10, 9, 8, 7 }.Select(p => Item(null, p, maxPoints: 10)).ToList();

        var result = CustomerFitRules.Calculate(items, FitBands);

        Assert.Equal(new CustomerFitResultDto(85, 100, 10, 10, CustomerFitRating.Fit), result);
    }

    [Fact]
    public void CustomerFit_OneUnanswered_HasNoRating()
    {
        var items = new int?[] { 10, 10, null }.Select(p => Item(null, p, maxPoints: 10)).ToList();

        var result = CustomerFitRules.Calculate(items, FitBands);

        Assert.Equal(new CustomerFitResultDto(20, 30, 2, 3, null), result);
    }

    [Fact]
    public void CustomerFit_NotListed_HasNoRating()
    {
        // Internal and affiliated requests list no survey questions.
        Assert.Equal(new CustomerFitResultDto(0, 0, 0, 0, null), CustomerFitRules.Calculate([], FitBands));
    }
}
