using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.QuestionBank;
using Isdlc.Application.QuestionBank.Dtos;
using Isdlc.Domain.Projects;
using Isdlc.Domain.QuestionBank;

namespace Isdlc.Application.Tests.QuestionBank;

/// <summary>The shared form rules without a database. Expectations: docs/question-bank-api.md → Ortak kurallar.</summary>
public class QuestionnaireRulesTests
{
    // The risk analysis form's shared options: two scored, one asking for a note.
    private static readonly QuestionOptionDto RiskPresent = new(1, "risk-present", "Risk Var", null, OptionMeaning.Negative, true, true);
    private static readonly QuestionOptionDto Uncertain = new(2, "uncertain", "Belirsiz", null, OptionMeaning.Open, true, false);
    private static readonly QuestionOptionDto NoRisk = new(3, "no-risk", "Risk Yok", null, OptionMeaning.Complete, false, false);
    private static readonly QuestionOptionDto NotApplicable = new(4, "na", "Uygulanabilir Değil", null, OptionMeaning.NotApplicable, false, false);
    private static readonly IReadOnlyList<QuestionOptionDto> RiskOptions = [RiskPresent, Uncertain, NoRisk, NotApplicable];

    private static QuestionDto QuestionWith(int id, QuestionRequirement? requirement = QuestionRequirement.Required) =>
        new(id, $"risk-analysis/new-software-project/all/{id:000}", id, null, null, null, $"Soru {id}?", requirement,
            null, null, null, null, RiskOptions);

    private static QuestionnaireItemDto Item(QuestionDto question, int? optionId = null, string? note = null) =>
        new(question, optionId is null && note is null ? null : new AnswerDto(optionId, null, note, null, null, null, null));

    // ---- Listing ----

    private static Question Seeded(string workType = QuestionnaireRules.DefaultWorkType, RequestSource? subcategory = null, bool isActive = true) =>
        new() { WorkType = workType, Subcategory = subcategory, IsActive = isActive };

    [Theory]
    [InlineData(null, RequestSource.Internal, true)]                       // "TÜM ALT KATEGORİLER": every subcategory
    [InlineData(RequestSource.External, RequestSource.External, true)]
    [InlineData(RequestSource.External, RequestSource.Internal, false)]
    [InlineData(RequestSource.Affiliated, RequestSource.Affiliated, true)]
    public void ListedIn_Subcategory_ListsOwnAndAllSubcategoryQuestions(RequestSource? questionSub, RequestSource formSub, bool listed)
    {
        var rule = QuestionnaireRules.ListedIn(QuestionnaireRules.DefaultWorkType, formSub).Compile();

        Assert.Equal(listed, rule(Seeded(subcategory: questionSub)));
    }

    [Fact]
    public void ListedIn_InactiveOrOtherWorkTypeQuestion_IsNotListed()
    {
        var rule = QuestionnaireRules.ListedIn(QuestionnaireRules.DefaultWorkType, RequestSource.Internal).Compile();

        Assert.False(rule(Seeded(isActive: false)));
        Assert.False(rule(Seeded(workType: "add-on")));
    }

    // ---- Validation ----

    [Fact]
    public void Validate_OptionOfAnotherQuestion_Throws()
    {
        var ex = Assert.Throws<BusinessRuleException>(() =>
            QuestionnaireRules.Validate(QuestionWith(1), new SaveAnswerDto { OptionId = 99 }));

        Assert.Equal("Seçilen cevap bu soruya ait değil.", ex.Message);
    }

    [Theory]
    [InlineData(3)]     // "Risk Yok" is not scored
    [InlineData(null)]  // no option at all
    public void Validate_ProbabilityWithoutScoredOption_Throws(int? optionId)
    {
        var ex = Assert.Throws<BusinessRuleException>(() =>
            QuestionnaireRules.Validate(QuestionWith(1), new SaveAnswerDto { OptionId = optionId, Probability = 2 }));

        Assert.Equal("Olasılık ve şiddet yalnızca puanlanan cevaplarda girilir.", ex.Message);
    }

    [Fact]
    public void Validate_SeverityWithUnscoredOption_Throws()
    {
        Assert.Throws<BusinessRuleException>(() =>
            QuestionnaireRules.Validate(QuestionWith(1), new SaveAnswerDto { OptionId = NoRisk.Id, Severity = 1 }));
    }

    [Theory]
    [InlineData(1)]  // Risk Var
    [InlineData(2)]  // Belirsiz
    public void Validate_ScoredOptionWithProbabilityAndSeverity_IsValid(int optionId)
    {
        QuestionnaireRules.Validate(QuestionWith(1), new SaveAnswerDto { OptionId = optionId, Probability = 2, Severity = 3 });
    }

    [Fact]
    public void Validate_NoteOnlyAnswer_IsValid()
    {
        QuestionnaireRules.Validate(QuestionWith(1), new SaveAnswerDto { Note = "Bilgi bekleniyor." });
    }

    [Fact]
    public void IsEmpty_OnlyWhitespace_IsEmpty_AnyValue_IsNot()
    {
        Assert.True(QuestionnaireRules.IsEmpty(new SaveAnswerDto { Text = " ", Note = "" }));
        Assert.False(QuestionnaireRules.IsEmpty(new SaveAnswerDto { OptionId = 1 }));
        Assert.False(QuestionnaireRules.IsEmpty(new SaveAnswerDto { Note = "x" }));
        Assert.False(QuestionnaireRules.IsEmpty(new SaveAnswerDto { Severity = 1 }));
    }

    // ---- Summary ----

    [Fact]
    public void Summarize_NothingAnswered_CountsEveryRequiredQuestion()
    {
        var summary = QuestionnaireRules.Summarize([
            Item(QuestionWith(1)),
            Item(QuestionWith(2, QuestionRequirement.Conditional)),
            Item(QuestionWith(3, requirement: null))]);   // forms without the column: every question has to be answered

        Assert.Equal(new QuestionnaireSummaryDto(3, 0, 2, false, new OptionMeaningCountsDto(0, 0, 0, 0, 0), 0), summary);
    }

    [Fact]
    public void Summarize_RequiredAnswered_ConditionalLeftOpen_IsComplete()
    {
        var summary = QuestionnaireRules.Summarize([
            Item(QuestionWith(1), NoRisk.Id),
            Item(QuestionWith(2, QuestionRequirement.Conditional))]);

        Assert.True(summary.IsComplete);
        Assert.Equal(0, summary.RequiredUnanswered);
        Assert.Equal(1, summary.Answered);
    }

    [Fact]
    public void Summarize_NoteWithoutOption_IsNotAnswered()
    {
        var summary = QuestionnaireRules.Summarize([Item(QuestionWith(1), note: "Toplantıda sorulacak.")]);

        Assert.Equal(0, summary.Answered);
        Assert.False(summary.IsComplete);
    }

    [Fact]
    public void Summarize_CountsMeanings_AndChosenOptionsMissingTheirNote()
    {
        var summary = QuestionnaireRules.Summarize([
            Item(QuestionWith(1), RiskPresent.Id),                       // note asked, missing
            Item(QuestionWith(2), RiskPresent.Id, note: "ERP gecikebilir."),
            Item(QuestionWith(3), Uncertain.Id),
            Item(QuestionWith(4), NoRisk.Id),
            Item(QuestionWith(5), NotApplicable.Id)]);

        Assert.Equal(new OptionMeaningCountsDto(Complete: 1, Partial: 0, Open: 1, Negative: 2, NotApplicable: 1), summary.ByMeaning);
        Assert.Equal(1, summary.MissingNote);
        Assert.Equal(5, summary.Answered);
        Assert.True(summary.IsComplete);
    }

    [Fact]
    public void Summarize_LevelOptionsWithoutMeaning_AreAnsweredButNotInMeanings()
    {
        var levelOption = new QuestionOptionDto(10, "x/1", "Tek iş senaryosu", 1, null, false, false);
        var question = QuestionWith(1, requirement: null) with { Options = [levelOption] };

        var summary = QuestionnaireRules.Summarize([Item(question, levelOption.Id)]);

        Assert.Equal(1, summary.Answered);
        Assert.True(summary.IsComplete);
        Assert.Equal(new OptionMeaningCountsDto(0, 0, 0, 0, 0), summary.ByMeaning);
    }
}
