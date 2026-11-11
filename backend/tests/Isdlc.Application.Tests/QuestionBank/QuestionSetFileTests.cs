using Isdlc.Domain.Projects;
using Isdlc.Domain.QuestionBank;
using Isdlc.Infrastructure.QuestionBank;

namespace Isdlc.Application.Tests.QuestionBank;

/// <summary>
/// The question set seed files (no database): the real files in the repo are valid and match the company's forms,
/// and the validator catches broken files. Expectations: docs/question-bank-api.md.
/// </summary>
public class QuestionSetFileTests
{
    private const string NewProject = "new-software-project";

    private static readonly IReadOnlyList<QuestionSetFile> Files = QuestionSetFile.ReadAll();

    private static QuestionSetFile File(string code) => Assert.Single(Files, f => f.Code == code);

    /// <summary>The questions a form lists for one subcategory: its own plus the ones for every subcategory.</summary>
    private static int ListedFor(QuestionSetFile file, RequestSource subcategory) =>
        file.Questions.Count(q => q.WorkType == NewProject && (q.Subcategory is null || q.Subcategory == subcategory));

    // ---- The real files ----

    [Fact]
    public void ReadAll_EmbeddedFiles_AreTheFourForms()
    {
        Assert.Equal(["internal-approval", "level", "request-analysis", "risk-analysis"], Files.Select(f => f.Code));
    }

    [Fact]
    public void Validate_EmbeddedFiles_HaveNoErrors()
    {
        Assert.Empty(QuestionSetFileValidator.Validate(Files));
    }

    [Theory]
    // Question counts of each form for internal / external / affiliated requests (the forms' "Soru Sayısı").
    [InlineData("request-analysis", 63, 69, 63)]
    [InlineData("level", 24, 26, 24)]
    [InlineData("risk-analysis", 45, 46, 45)]
    [InlineData("internal-approval", 39, 39, 39)]
    public void EmbeddedFiles_ListAsManyQuestionsAsTheForms(string code, int internalCount, int externalCount, int affiliatedCount)
    {
        var file = File(code);

        Assert.Equal(internalCount, ListedFor(file, RequestSource.Internal));
        Assert.Equal(externalCount, ListedFor(file, RequestSource.External));
        Assert.Equal(affiliatedCount, ListedFor(file, RequestSource.Affiliated));
    }

    [Fact]
    public void Level_EveryQuestion_HasFiveOptionsWithPoints_AndTwoSectionsUpTo60()
    {
        var file = File("level");

        Assert.Equal(AnswerMode.QuestionOptions, file.AnswerMode);
        Assert.All(file.Questions, q => Assert.Equal(5, q.Options!.Count));
        Assert.Equal(["1", "2"], file.Questions.Select(q => q.Section).Distinct().Order());

        // The highest possible sum of each section is 60, the end of the form's last range.
        foreach (var sub in new[] { RequestSource.Internal, RequestSource.External, RequestSource.Affiliated })
        {
            foreach (var section in new[] { "1", "2" })
            {
                var max = file.Questions.Where(q => q.Subcategory == sub && q.Section == section).Sum(q => q.Options!.Max(o => o.Points));
                Assert.Equal(60, max);
            }
        }

        Assert.Equal(60, file.Bands!.Single(b => b.Section == "1").Ranges[^1].Max);
        Assert.Equal(60, file.Bands!.Single(b => b.Section == "2").Ranges[^1].Max);
    }

    [Fact]
    public void RiskAnalysis_RiskAndUncertainAreScored_AndDegreesCover1To9()
    {
        var file = File("risk-analysis");

        Assert.Equal(["Risk Var", "Belirsiz (Bilgi Eksik)"], file.Options.Where(o => o.IsScored).Select(o => o.Label));
        var ranges = Assert.Single(file.Bands!).Ranges;
        Assert.Equal(1, ranges[0].Min);
        Assert.Equal(9, ranges[^1].Max);
        // 2 × 2 = 4 is "Orta Dereceli" on the form's matrix.
        Assert.Equal("Orta Dereceli", ranges.Single(r => r.Min <= 4 && 4 <= r.Max).Result);
    }

    [Theory]
    [InlineData("request-analysis")]
    [InlineData("risk-analysis")]
    [InlineData("internal-approval")]
    public void SharedOptionSets_CoverEveryMeaningTheFormUses_AndNotApplicable(string code)
    {
        var meanings = File(code).Options.Select(o => o.Meaning).ToList();

        Assert.Contains(OptionMeaning.Complete, meanings);
        Assert.Contains(OptionMeaning.NotApplicable, meanings);
        Assert.Equal(meanings.Count, meanings.Distinct().Count());
    }

    [Fact]
    public void QuestionKey_IsSetWorkTypeSubcategoryAndNumber()
    {
        var file = File("risk-analysis");

        Assert.Equal("risk-analysis/new-software-project/all/001", file.QuestionKey(file.Questions.First(q => q.No == 1)));
        var affiliated = file.Questions.First(q => q.Subcategory == RequestSource.Affiliated);
        Assert.Equal($"risk-analysis/new-software-project/affiliated/{affiliated.No:000}", file.QuestionKey(affiliated));
    }

    // ---- The validator catches broken files ----

    private static QuestionSetFile SharedSet(List<QuestionSeed>? questions = null, List<BandSeed>? bands = null) => new(
        "test-set", "Test", "ISDLC-Test", "1.0", AnswerMode.SharedOptions,
        [new SharedOptionSeed("yes", "Evet", OptionMeaning.Complete, false, false)],
        bands,
        questions ?? [new QuestionSeed(NewProject, null, 1, 1, "Soru?")]);

    [Fact]
    public void Validate_ValidFile_HasNoErrors()
    {
        Assert.Empty(QuestionSetFileValidator.Validate([SharedSet()]));
    }

    [Fact]
    public void Validate_SameSetTwice_IsAnError()
    {
        Assert.Contains(QuestionSetFileValidator.Validate([SharedSet(), SharedSet()]), e => e.Contains("more than one file"));
    }

    [Fact]
    public void Validate_RepeatingQuestionKey_IsAnError()
    {
        var file = SharedSet([new QuestionSeed(NewProject, null, 1, 1, "A?"), new QuestionSeed(NewProject, null, 1, 2, "B?")]);

        Assert.Contains(QuestionSetFileValidator.Validate([file]), e => e.Contains("repeats"));
    }

    [Fact]
    public void Validate_SameNumberInAnotherSubcategory_IsNotAnError()
    {
        var file = SharedSet([
            new QuestionSeed(NewProject, RequestSource.Internal, 1, 1, "A?"),
            new QuestionSeed(NewProject, RequestSource.External, 1, 2, "B?")]);

        Assert.Empty(QuestionSetFileValidator.Validate([file]));
    }

    [Fact]
    public void Validate_EmptyOrTooLongText_IsAnError()
    {
        Assert.NotEmpty(QuestionSetFileValidator.Validate([SharedSet([new QuestionSeed(NewProject, null, 1, 1, " ")])]));
        Assert.NotEmpty(QuestionSetFileValidator.Validate([SharedSet([new QuestionSeed(NewProject, null, 1, 1, new string('a', 2001))])]));
    }

    [Fact]
    public void Validate_SharedSetWithoutOptions_IsAnError()
    {
        var file = SharedSet() with { Options = [] };

        Assert.Contains(QuestionSetFileValidator.Validate([file]), e => e.Contains("shared options are missing"));
    }

    [Fact]
    public void Validate_QuestionOptionSetWithoutQuestionOptions_IsAnError()
    {
        var file = SharedSet() with { AnswerMode = AnswerMode.QuestionOptions, Options = [] };

        Assert.Contains(QuestionSetFileValidator.Validate([file]), e => e.Contains("options are missing"));
    }

    [Fact]
    public void Validate_QuestionWithOwnOptionsInASharedSet_IsAnError()
    {
        var file = SharedSet([new QuestionSeed(NewProject, null, 1, 1, "A?", Options: [new QuestionOptionSeed("x", 1)])]);

        Assert.Contains(QuestionSetFileValidator.Validate([file]), e => e.Contains("cannot have its own"));
    }

    [Theory]
    [InlineData(1, 10, 12, 20)]   // gap: 11 is missing
    [InlineData(1, 10, 10, 20)]   // overlap: 10 is in both
    public void Validate_BandsWithGapOrOverlap_IsAnError(int min1, int max1, int min2, int max2)
    {
        var file = SharedSet(bands: [new BandSeed("1", "Seviye", [new(min1, max1, "1"), new(min2, max2, "2")])]);

        Assert.Contains(QuestionSetFileValidator.Validate([file]), e => e.Contains("gap or overlap"));
    }
}
