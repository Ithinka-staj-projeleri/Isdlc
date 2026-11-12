using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.QuestionBank;
using Isdlc.Application.QuestionBank.Dtos;
using Isdlc.Application.Tests.Common;
using Isdlc.Application.Tests.Identity;
using Isdlc.Domain.Projects;
using Isdlc.Domain.QuestionBank;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.QuestionBank;

/// <summary>
/// The question bank and project forms against the contract (docs/question-bank-api.md) on the real seeded sets:
/// the form tables, completion, note warnings, who may write, and that forms of other sets and projects stay apart.
/// Every test that writes creates its own project. Cross test (PLANNING.md): Erva → Baran's modules.
/// </summary>
[Collection("Database")]
public class QuestionnaireCrossTests(DatabaseFixture database)
{
    private const string Level = "level";
    private const string RequestAnalysis = "request-analysis";
    private const string Risk = "risk-analysis";
    private const string InternalApproval = "internal-approval";

    /// <summary>Any step the project can wait at; the forms do not depend on the step.</summary>
    private const string Step = "5";

    // ---- Helpers ----

    private async Task<T> AsAsync<T>(string? email, Func<IQuestionnaireService, Task<T>> action)
    {
        var userId = email is null ? (int?)null : await TestProjects.UserIdAsync(database, email);
        return await AsUserAsync(userId, action);
    }

    private async Task<T> AsUserAsync<T>(int? userId, Func<IQuestionnaireService, Task<T>> action)
    {
        await using var scope = database.CreateScope();
        var service = new QuestionnaireService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(), new TestUser(userId));
        return await action(service);
    }

    private async Task<IReadOnlyList<QuestionDto>> ListAsync(string set, RequestSource subcategory, string? workType = null)
    {
        await using var scope = database.CreateScope();
        var service = new QuestionBankService(scope.ServiceProvider.GetRequiredService<IApplicationDbContext>());
        return await service.ListQuestionsAsync(set, new QuestionFilter { Subcategory = subcategory, WorkType = workType }, default);
    }

    private Task<QuestionnaireDto> GetAsync(int projectId, string set) =>
        AsAsync(TestProjects.Analyst, s => s.GetAsync(projectId, set, default));

    private Task<QuestionnaireDto> AnswerAsync(int projectId, string set, int questionId, SaveAnswerDto answer, string email = TestProjects.Analyst) =>
        AsAsync(email, s => s.SaveAnswerAsync(projectId, set, questionId, answer, default));

    private Task<QuestionnaireDto> SaveHeaderAsync(int projectId, string set, SaveQuestionnaireDto header) =>
        AsAsync(TestProjects.Analyst, s => s.SaveAsync(projectId, set, header, default));

    private static int OptionId(QuestionDto question, string code) => question.Options.Single(o => o.Code == code).Id;

    private static AnswerDto? AnswerOf(QuestionnaireDto form, int questionId) =>
        form.Questions.Single(i => i.Question.Id == questionId).Answer;

    /// <summary>A new active project whose request source (the forms' subcategory) is the given one.</summary>
    private async Task<int> CreateProjectAsync(RequestSource source = RequestSource.Internal)
    {
        var projectId = await TestProjects.CreateAsync(database, Step);
        if (source != RequestSource.Internal)
        {
            await using var scope = database.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var project = await db.Projects.SingleAsync(p => p.Id == projectId);
            project.Source = source;
            await db.SaveChangesAsync();
        }

        return projectId;
    }

    // ---- The forms as the contract describes them ----

    [Theory]
    // Set, subcategory, listed, required, conditional (docs/question-bank-api.md → "Formlardan çıkan ortak yapı").
    [InlineData(RequestAnalysis, RequestSource.Internal, 63, 33, 30)]
    [InlineData(RequestAnalysis, RequestSource.External, 69, 38, 31)]
    [InlineData(RequestAnalysis, RequestSource.Affiliated, 63, 33, 30)]
    [InlineData(Level, RequestSource.Internal, 24, 0, 0)]
    [InlineData(Level, RequestSource.External, 26, 0, 0)]
    [InlineData(Level, RequestSource.Affiliated, 24, 0, 0)]
    [InlineData(Risk, RequestSource.Internal, 45, 31, 14)]
    [InlineData(Risk, RequestSource.External, 46, 31, 15)]
    [InlineData(Risk, RequestSource.Affiliated, 45, 30, 15)]
    [InlineData(InternalApproval, RequestSource.Internal, 39, 0, 0)]
    [InlineData(InternalApproval, RequestSource.External, 39, 0, 0)]
    [InlineData(InternalApproval, RequestSource.Affiliated, 39, 0, 0)]
    public async Task ListQuestions_EverySetAndSubcategory_MatchesTheFormsCounts(
        string set, RequestSource subcategory, int listed, int required, int conditional)
    {
        var questions = await ListAsync(set, subcategory);

        Assert.Equal(listed, questions.Count);
        Assert.Equal(required, questions.Count(q => q.Requirement == QuestionRequirement.Required));
        Assert.Equal(conditional, questions.Count(q => q.Requirement == QuestionRequirement.Conditional));
        Assert.Equal(questions.Count, questions.Select(q => q.Id).Distinct().Count());
    }

    [Theory]
    // The shared options and their meanings (docs/question-bank-api.md → "Seçeneğin anlamı").
    [InlineData(RequestAnalysis, "Yanıtlandı=Complete | Kısmen Yanıtlandı=Partial | Açık Konu Var=Open | Uygulanabilir Değil=NotApplicable")]
    [InlineData(Risk, "Risk Var=Negative | Belirsiz (Bilgi Eksik)=Open | Risk Yok=Complete | Uygulanabilir Değil=NotApplicable")]
    [InlineData(InternalApproval, "Uygun=Complete | Kısmen Uygun=Partial | Uygun Değil=Negative | Uygulanamaz=NotApplicable")]
    public async Task ListQuestions_SharedOptionSets_HaveTheContractsMeanings(string set, string expected)
    {
        var options = (await ListAsync(set, RequestSource.External))[0].Options;

        Assert.Equal(expected, string.Join(" | ", options.Select(o => $"{o.Label}={o.Meaning}")));
    }

    [Fact]
    public async Task ListQuestions_OnlyRiskPresentAndUncertain_AreScored()
    {
        Assert.Equal(["risk-present", "uncertain"],
            (await ListAsync(Risk, RequestSource.Internal))[0].Options.Where(o => o.IsScored).Select(o => o.Code));

        foreach (var set in new[] { Level, RequestAnalysis, InternalApproval })
        {
            Assert.DoesNotContain((await ListAsync(set, RequestSource.Internal)).SelectMany(q => q.Options), o => o.IsScored);
        }
    }

    [Fact]
    public async Task ListQuestions_LevelOptions_HaveNoMeaningAndAreOwnedByEachQuestion()
    {
        var questions = await ListAsync(Level, RequestSource.Internal);

        Assert.All(questions.SelectMany(q => q.Options), o => Assert.Null(o.Meaning));
        Assert.Equal(questions.Count * 5, questions.SelectMany(q => q.Options).Select(o => o.Id).Distinct().Count());
    }

    [Fact]
    public async Task ListQuestions_NoWorkType_IsTheNewSoftwareProject_AnotherFlowHasNoQuestionsYet()
    {
        var byDefault = await ListAsync(Risk, RequestSource.Internal);
        var named = await ListAsync(Risk, RequestSource.Internal, QuestionnaireRules.DefaultWorkType);

        Assert.Equal(named.Select(q => q.Id), byDefault.Select(q => q.Id));
        Assert.Empty(await ListAsync(Risk, RequestSource.Internal, "add-on")); // decision 4: no add-on questions in the forms
    }

    // ---- Reading a project's form ----

    [Fact]
    public async Task Get_ExternalProject_ListsTheExternalQuestions_AndCountsRequiredOnly()
    {
        var projectId = await CreateProjectAsync(RequestSource.External);

        var form = await GetAsync(projectId, Risk);

        Assert.Equal(RequestSource.External, form.Subcategory);
        Assert.Equal(46, form.Summary.Listed);
        Assert.Equal(31, form.Summary.RequiredUnanswered); // the 15 conditional questions do not block completion
        Assert.False(form.Summary.IsComplete);
    }

    [Fact]
    public async Task Get_FormWithoutRequirementColumn_CountsEveryQuestionAsRequired()
    {
        var projectId = await CreateProjectAsync();

        var form = await GetAsync(projectId, Level);

        Assert.Equal(24, form.Summary.Listed);
        Assert.Equal(24, form.Summary.RequiredUnanswered);
    }

    [Fact]
    public async Task Get_ClosedProject_CanStillBeRead_EvenWithoutSignIn()
    {
        var projectId = await TestProjects.CreateAsync(database, null, ProjectStatus.Closed);

        var form = await AsAsync(null, s => s.GetAsync(projectId, RequestAnalysis, default));

        Assert.Equal(63, form.Summary.Listed);
    }

    [Fact]
    public async Task Get_UnknownSet_Throws404()
    {
        var projectId = await CreateProjectAsync();

        var ex = await Assert.ThrowsAsync<NotFoundException>(() => GetAsync(projectId, "no-such-set"));
        Assert.Equal("Soru seti bulunamadı.", ex.Message);
    }

    // ---- Completion and warnings ----

    [Fact]
    public async Task SaveAnswer_EveryRequiredAnswered_CompletesTheForm_WithConditionalQuestionsLeftOpen()
    {
        var projectId = await CreateProjectAsync();
        var required = (await GetAsync(projectId, Risk)).Questions
            .Select(i => i.Question)
            .Where(q => q.Requirement == QuestionRequirement.Required)
            .ToList();

        var form = await GetAsync(projectId, Risk);
        foreach (var question in required)
        {
            Assert.False(form.Summary.IsComplete);
            form = await AnswerAsync(projectId, Risk, question.Id, new SaveAnswerDto { OptionId = OptionId(question, "no-risk") });
        }

        Assert.True(form.Summary.IsComplete);
        Assert.Equal(0, form.Summary.RequiredUnanswered);
        Assert.Equal(31, form.Summary.Answered);
        Assert.Equal(31, form.Summary.ByMeaning.Complete);
    }

    [Fact]
    public async Task SaveAnswer_ConditionalClosedAsNotApplicable_IsCountedAsNotApplicable()
    {
        var projectId = await CreateProjectAsync();
        var conditional = (await GetAsync(projectId, Risk)).Questions
            .Select(i => i.Question)
            .First(q => q.Requirement == QuestionRequirement.Conditional);

        var form = await AnswerAsync(projectId, Risk, conditional.Id,
            new SaveAnswerDto { OptionId = OptionId(conditional, "not-applicable") });

        Assert.Equal(1, form.Summary.ByMeaning.NotApplicable);
        Assert.Equal(31, form.Summary.RequiredUnanswered);
        Assert.Equal(0, form.Summary.MissingNote); // "Uygulanabilir Değil" asks for no note in risk analysis
    }

    [Fact]
    public async Task SaveAnswer_OptionAskingForANote_WithoutOne_IsSavedButCounted_UntilTheNoteIsWritten()
    {
        var projectId = await CreateProjectAsync();
        var question = (await GetAsync(projectId, InternalApproval)).Questions[0].Question;
        var notCompliant = OptionId(question, "not-compliant");

        var withoutNote = await AnswerAsync(projectId, InternalApproval, question.Id, new SaveAnswerDto { OptionId = notCompliant });
        var withNote = await AnswerAsync(projectId, InternalApproval, question.Id,
            new SaveAnswerDto { OptionId = notCompliant, Note = "Erişilebilirlik maddesi yazılmamış." });

        Assert.Equal(1, withoutNote.Summary.MissingNote);
        Assert.Equal(1, withoutNote.Summary.Answered);
        Assert.Equal(0, withNote.Summary.MissingNote);
        Assert.Equal(1, withNote.Summary.ByMeaning.Negative);
    }

    [Fact]
    public async Task SaveAnswer_ScoredRiskWithoutProbability_IsAllowed()
    {
        // The degree is worked out later (B4), which counts rows missing their scores; saving is not blocked.
        var projectId = await CreateProjectAsync();
        var question = (await GetAsync(projectId, Risk)).Questions[0].Question;

        var form = await AnswerAsync(projectId, Risk, question.Id,
            new SaveAnswerDto { OptionId = OptionId(question, "uncertain"), Note = "Servis listesi bekleniyor." });

        var answer = AnswerOf(form, question.Id)!;
        Assert.Null(answer.Probability);
        Assert.Null(answer.Severity);
        Assert.Equal(1, form.Summary.ByMeaning.Open);
    }

    [Fact]
    public async Task SaveAnswer_ScoredRiskChangedToNoRisk_DropsTheScores()
    {
        var projectId = await CreateProjectAsync();
        var question = (await GetAsync(projectId, Risk)).Questions[0].Question;
        await AnswerAsync(projectId, Risk, question.Id,
            new SaveAnswerDto { OptionId = OptionId(question, "risk-present"), Probability = 3, Severity = 3 });

        var form = await AnswerAsync(projectId, Risk, question.Id, new SaveAnswerDto { OptionId = OptionId(question, "no-risk") });

        var answer = AnswerOf(form, question.Id)!;
        Assert.Equal(OptionId(question, "no-risk"), answer.OptionId);
        Assert.Null(answer.Probability);
        Assert.Null(answer.Severity);
        Assert.Equal(0, form.Summary.ByMeaning.Negative);
    }

    [Fact]
    public async Task SaveAnswer_StakeholderReplyWithoutStatus_IsKeptButNotAnswered()
    {
        var projectId = await CreateProjectAsync();
        var question = (await GetAsync(projectId, RequestAnalysis)).Questions[0].Question;

        var form = await AnswerAsync(projectId, RequestAnalysis, question.Id,
            new SaveAnswerDto { Text = "  Saha ekibi stok sayımını kâğıtla yapıyor.  " });

        Assert.Equal("Saha ekibi stok sayımını kâğıtla yapıyor.", AnswerOf(form, question.Id)!.Text);
        Assert.Equal(0, form.Summary.Answered);
    }

    [Fact]
    public async Task SaveAnswer_LevelOptionOfAnotherLevelQuestion_Throws400()
    {
        var projectId = await CreateProjectAsync();
        var questions = (await GetAsync(projectId, Level)).Questions.Select(i => i.Question).ToList();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            AnswerAsync(projectId, Level, questions[0].Id, new SaveAnswerDto { OptionId = questions[1].Options[0].Id }));
        Assert.Equal("Seçilen cevap bu soruya ait değil.", ex.Message);
    }

    // ---- Forms stay apart ----

    [Fact]
    public async Task SaveAnswer_OnOneSet_DoesNotTouchTheProjectsOtherForms_OrOtherProjects()
    {
        var projectId = await CreateProjectAsync();
        var otherProjectId = await CreateProjectAsync();
        var question = (await GetAsync(projectId, Risk)).Questions[0].Question;

        await AnswerAsync(projectId, Risk, question.Id,
            new SaveAnswerDto { OptionId = OptionId(question, "risk-present"), Note = "ERP dokümanı yok." });

        Assert.Equal(0, (await GetAsync(otherProjectId, Risk)).Summary.Answered);
        foreach (var set in new[] { Level, RequestAnalysis, InternalApproval })
        {
            Assert.Equal(0, (await GetAsync(projectId, set)).Summary.Answered);
        }

        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        Assert.Equal(1, await db.Questionnaires.CountAsync(q => q.ProjectId == projectId));
        Assert.False(await db.Questionnaires.AnyAsync(q => q.ProjectId == otherProjectId));
    }

    // ---- Who may write, and where ----

    [Theory]
    [InlineData(ProjectStatus.Rejected)]
    [InlineData(ProjectStatus.Lost)]
    [InlineData(ProjectStatus.Redirected)]
    public async Task SaveAnswer_ProjectNotActive_Throws409(ProjectStatus status)
    {
        var projectId = await TestProjects.CreateAsync(database, null, status);
        var question = (await GetAsync(projectId, Level)).Questions[0].Question;

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            AnswerAsync(projectId, Level, question.Id, new SaveAnswerDto { OptionId = question.Options[0].Id }));
        Assert.Equal("Proje aktif değil; kayıt eklenemez veya değiştirilemez.", ex.Message);
    }

    [Fact]
    public async Task SaveAnswer_UnknownProjectOrSet_Throws404()
    {
        var projectId = await CreateProjectAsync();
        var question = (await GetAsync(projectId, Level)).Questions[0].Question;

        await Assert.ThrowsAsync<NotFoundException>(() =>
            AnswerAsync(int.MaxValue, Level, question.Id, new SaveAnswerDto { OptionId = question.Options[0].Id }));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            AnswerAsync(projectId, "no-such-set", question.Id, new SaveAnswerDto { OptionId = question.Options[0].Id }));
    }

    [Fact]
    public async Task Save_ClosedProject_Throws409()
    {
        var projectId = await TestProjects.CreateAsync(database, null, ProjectStatus.Closed);

        await Assert.ThrowsAsync<ConflictException>(() =>
            SaveHeaderAsync(projectId, Level, new SaveQuestionnaireDto { Subcategory = RequestSource.Internal }));
    }

    [Fact]
    public async Task Save_NotSignedIn_Throws403_AndCreatesNoForm()
    {
        var projectId = await CreateProjectAsync();

        await Assert.ThrowsAsync<ForbiddenException>(() => AsAsync(null,
            s => s.SaveAsync(projectId, Level, new SaveQuestionnaireDto { Subcategory = RequestSource.Internal }, default)));

        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        Assert.False(await db.Questionnaires.AnyAsync(q => q.ProjectId == projectId));
    }

    // ---- Header ----

    [Fact]
    public async Task Save_InactiveApprover_Throws400()
    {
        var projectId = await CreateProjectAsync();
        var inactive = await TestUsers.CreateAsync(database, password: null, isActive: false);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => SaveHeaderAsync(projectId, InternalApproval,
            new SaveQuestionnaireDto { Subcategory = RequestSource.Internal, ApprovedByUserId = inactive.Id }));
        Assert.Equal("Seçilen onaylayan aktif bir kullanıcı değil.", ex.Message);
    }

    [Fact]
    public async Task Save_ApproverLeftEmpty_ClearsTheEarlierApprover_AndBlankParticipantsAreNotKept()
    {
        var projectId = await CreateProjectAsync();
        var managerId = await TestProjects.UserIdAsync(database, TestProjects.Manager);
        await SaveHeaderAsync(projectId, InternalApproval, new SaveQuestionnaireDto
        {
            Subcategory = RequestSource.Internal, Participants = "Elif Yılmaz", ApprovedByUserId = managerId, ApprovedOn = new DateOnly(2026, 11, 12),
        });

        var form = await SaveHeaderAsync(projectId, InternalApproval,
            new SaveQuestionnaireDto { Subcategory = RequestSource.Internal, Participants = "   " });

        Assert.Null(form.ApprovedBy);
        Assert.Null(form.ApprovedOn);
        Assert.Null(form.Participants);
    }

    [Fact]
    public async Task Save_SubcategoryBeforeAnyAnswer_CreatesTheForm_AndItsOwnQuestionsCanBeAnswered()
    {
        var projectId = await CreateProjectAsync(); // internal request
        var externalOnly = (await ListAsync(Level, RequestSource.External))
            .ExceptBy((await ListAsync(Level, RequestSource.Internal)).Select(q => q.Id), q => q.Id)
            .First();

        var header = await SaveHeaderAsync(projectId, Level, new SaveQuestionnaireDto { Subcategory = RequestSource.External });
        var form = await AnswerAsync(projectId, Level, externalOnly.Id, new SaveAnswerDto { OptionId = externalOnly.Options[0].Id });

        Assert.Equal(RequestSource.External, header.Subcategory);
        Assert.Equal(26, header.Summary.Listed);
        Assert.Equal(1, form.Summary.Answered);
        Assert.Equal(RequestSource.External, (await GetAsync(projectId, Level)).Subcategory); // the form no longer follows the project
    }
}
