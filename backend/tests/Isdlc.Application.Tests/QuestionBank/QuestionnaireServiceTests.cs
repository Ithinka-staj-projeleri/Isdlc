using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.QuestionBank;
using Isdlc.Application.QuestionBank.Dtos;
using Isdlc.Application.Tests.Common;
using Isdlc.Domain.Process;
using Isdlc.Domain.Projects;
using Isdlc.Domain.QuestionBank;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.QuestionBank;

/// <summary>
/// The question bank and project forms on a real database with the seeded sets. Every test that writes creates its
/// own project (TestProjects: an internal request), so seed projects are never changed. Expectations: docs/question-bank-api.md
/// </summary>
[Collection("Database")]
public class QuestionnaireServiceTests(DatabaseFixture database)
{
    private const string Risk = "risk-analysis";
    private const string Level = "level";

    private async Task<T> AsAsync<T>(string? email, Func<IQuestionnaireService, Task<T>> action)
    {
        var userId = email is null ? (int?)null : await TestProjects.UserIdAsync(database, email);
        await using var scope = database.CreateScope();
        var service = new QuestionnaireService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(), new TestUser(userId));
        return await action(service);
    }

    private async Task<T> BankAsync<T>(Func<IQuestionBankService, Task<T>> action)
    {
        await using var scope = database.CreateScope();
        return await action(new QuestionBankService(scope.ServiceProvider.GetRequiredService<IApplicationDbContext>()));
    }

    private Task<QuestionnaireDto> GetAsync(int projectId, string set = Risk) =>
        AsAsync(TestProjects.Analyst, s => s.GetAsync(projectId, set, default));

    private Task<QuestionnaireDto> AnswerAsync(int projectId, int questionId, SaveAnswerDto answer, string set = Risk) =>
        AsAsync(TestProjects.Analyst, s => s.SaveAnswerAsync(projectId, set, questionId, answer, default));

    private Task<QuestionnaireDto> SaveHeaderAsync(int projectId, SaveQuestionnaireDto header, string set = Risk) =>
        AsAsync(TestProjects.Analyst, s => s.SaveAsync(projectId, set, header, default));

    private static QuestionOptionDto OptionOf(QuestionDto question, string code) => question.Options.Single(o => o.Code == code);

    // ---- Question bank ----

    [Fact]
    public async Task ListSets_ReturnsTheFiveForms()
    {
        var sets = await BankAsync(s => s.ListSetsAsync(default));

        Assert.Equal(["customer-fit", "internal-approval", "level", "request-analysis", "risk-analysis"], sets.Select(s => s.Code).Order());
        Assert.Equal(AnswerMode.QuestionOptions, sets.Single(s => s.Code == Level).AnswerMode);
    }

    [Fact]
    public async Task ListQuestions_LevelExternal_Lists26QuestionsWithOwnPointedOptions()
    {
        var questions = await BankAsync(s => s.ListQuestionsAsync(Level, new QuestionFilter { Subcategory = RequestSource.External }, default));

        Assert.Equal(26, questions.Count);
        Assert.All(questions, q => Assert.Equal(5, q.Options.Count));
        Assert.All(questions.SelectMany(q => q.Options), o => Assert.NotNull(o.Points));
    }

    [Fact]
    public async Task ListQuestions_SharedSet_GivesEveryQuestionTheSetsOptionsInOrder()
    {
        var questions = await BankAsync(s => s.ListQuestionsAsync(Risk, new QuestionFilter { Subcategory = RequestSource.Internal }, default));

        Assert.Equal(45, questions.Count);
        Assert.Equal(questions.Select(q => q.Code).ToList().Count, questions.Select(q => q.Code).Distinct().Count());
        Assert.All(questions, q => Assert.Equal(["Risk Var", "Belirsiz (Bilgi Eksik)", "Risk Yok", "Uygulanabilir Değil"], q.Options.Select(o => o.Label)));
        Assert.DoesNotContain('/', questions[0].Options[0].Code);
    }

    [Fact]
    public async Task ListQuestions_UnknownSet_Throws404()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            BankAsync(s => s.ListQuestionsAsync("no-such-set", new QuestionFilter { Subcategory = RequestSource.Internal }, default)));
    }

    // ---- Get ----

    [Fact]
    public async Task Get_NothingSaved_ListsTheProjectsSubcategoryWithEmptyAnswers()
    {
        var projectId = await TestProjects.CreateAsync(database, "5");

        var form = await GetAsync(projectId);

        Assert.Equal(RequestSource.Internal, form.Subcategory);
        Assert.Equal(QuestionnaireRules.DefaultWorkType, form.WorkType);
        Assert.Equal(45, form.Questions.Count);
        Assert.All(form.Questions, item => Assert.Null(item.Answer));
        Assert.Equal(0, form.Summary.Answered);
        Assert.Null(form.Result);
    }

    [Fact]
    public async Task Get_AddOnProject_ListsTheNewSoftwareProjectQuestions()
    {
        var projectId = await TestProjects.CreateAsync(database, null, flowCode: ProcessFlowCodes.AddOn);

        var form = await GetAsync(projectId);

        Assert.Equal(ProcessFlowCodes.NewRequest, form.WorkType);
        Assert.Equal(45, form.Summary.Listed);
    }

    [Fact]
    public async Task Get_FlowWithoutForms_ListsNoQuestions_AndAnswersAreRefused()
    {
        var projectId = await TestProjects.CreateAsync(database, null, flowCode: ProcessFlowCodes.ProcessImprovement);
        var riskQuestion = (await BankAsync(s => s.ListQuestionsAsync(Risk, new QuestionFilter { Subcategory = RequestSource.Internal }, default)))[0];

        var form = await GetAsync(projectId);

        Assert.Equal(ProcessFlowCodes.ProcessImprovement, form.WorkType);
        Assert.Empty(form.Questions);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            AnswerAsync(projectId, riskQuestion.Id, new SaveAnswerDto { OptionId = riskQuestion.Options[0].Id }));
    }

    [Fact]
    public async Task Get_UnknownProject_Throws404()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => GetAsync(int.MaxValue));
    }

    // ---- Answers ----

    [Fact]
    public async Task SaveAnswer_ScoredRisk_IsSavedWithWhoAndWhen_AndCountedInTheSummary()
    {
        var projectId = await TestProjects.CreateAsync(database, "5");
        var question = (await GetAsync(projectId)).Questions[0].Question;

        var form = await AnswerAsync(projectId, question.Id, new SaveAnswerDto
        {
            OptionId = OptionOf(question, "risk-present").Id, Note = "  ERP servis dokümanı gelmedi. ", Probability = 2, Severity = 3,
        });

        var answer = form.Questions.Single(i => i.Question.Id == question.Id).Answer!;
        Assert.Equal("ERP servis dokümanı gelmedi.", answer.Note);
        Assert.Equal((2, 3), (answer.Probability, answer.Severity));
        Assert.Equal("Elif Yılmaz", answer.UpdatedBy!.FullName);
        Assert.NotNull(answer.UpdatedAt);
        Assert.Equal(1, form.Summary.Answered);
        Assert.Equal(1, form.Summary.ByMeaning.Negative);
    }

    [Fact]
    public async Task SaveAnswer_Changed_UpdatesTheSameRow_AndWritesTheAuditLog()
    {
        var projectId = await TestProjects.CreateAsync(database, "5");
        var question = (await GetAsync(projectId)).Questions[0].Question;

        await AnswerAsync(projectId, question.Id, new SaveAnswerDto { OptionId = OptionOf(question, "uncertain").Id });
        await AnswerAsync(projectId, question.Id, new SaveAnswerDto { OptionId = OptionOf(question, "no-risk").Id });

        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        Assert.Equal(1, await db.QuestionAnswers.CountAsync(a => a.ProjectId == projectId));
        Assert.Equal(1, await db.Questionnaires.CountAsync(q => q.ProjectId == projectId));
        Assert.True(await db.AuditLogs.AnyAsync(l => l.ProjectId == projectId && l.EntityName == nameof(QuestionAnswer)));
        Assert.True(await db.AuditLogs.AnyAsync(l => l.ProjectId == projectId && l.EntityName == nameof(Questionnaire)));
    }

    [Fact]
    public async Task SaveAnswer_AllEmpty_EmptiesTheAnswerWithoutDeletingIt()
    {
        var projectId = await TestProjects.CreateAsync(database, "5");
        var question = (await GetAsync(projectId)).Questions[0].Question;
        await AnswerAsync(projectId, question.Id, new SaveAnswerDto { OptionId = OptionOf(question, "no-risk").Id });

        var form = await AnswerAsync(projectId, question.Id, new SaveAnswerDto { Note = " " });

        var answer = form.Questions.Single(i => i.Question.Id == question.Id).Answer!;
        Assert.Null(answer.OptionId);
        Assert.Equal(0, form.Summary.Answered);
    }

    [Fact]
    public async Task SaveAnswer_EmptyOnANewForm_DoesNotCreateTheForm()
    {
        var projectId = await TestProjects.CreateAsync(database, "5");
        var question = (await GetAsync(projectId)).Questions[0].Question;

        await AnswerAsync(projectId, question.Id, new SaveAnswerDto());

        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        Assert.False(await db.Questionnaires.AnyAsync(q => q.ProjectId == projectId));
    }

    [Fact]
    public async Task SaveAnswer_ProbabilityOnUnscoredOption_Throws400()
    {
        var projectId = await TestProjects.CreateAsync(database, "5");
        var question = (await GetAsync(projectId)).Questions[0].Question;

        await Assert.ThrowsAsync<BusinessRuleException>(() => AnswerAsync(projectId, question.Id,
            new SaveAnswerDto { OptionId = OptionOf(question, "no-risk").Id, Probability = 1 }));
    }

    [Fact]
    public async Task SaveAnswer_OptionOfAnotherSet_Throws400()
    {
        var projectId = await TestProjects.CreateAsync(database, "5");
        var question = (await GetAsync(projectId)).Questions[0].Question;
        var levelOptionId = (await GetAsync(projectId, Level)).Questions[0].Question.Options[0].Id;

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            AnswerAsync(projectId, question.Id, new SaveAnswerDto { OptionId = levelOptionId }));
        Assert.Equal("Seçilen cevap bu soruya ait değil.", ex.Message);
    }

    [Fact]
    public async Task SaveAnswer_QuestionNotOnThisForm_Throws404()
    {
        var projectId = await TestProjects.CreateAsync(database, "5");
        var externalOnly = (await BankAsync(s => s.ListQuestionsAsync(Level, new QuestionFilter { Subcategory = RequestSource.External }, default)))[0];
        var riskQuestion = (await GetAsync(projectId)).Questions[0].Question;

        // A level question for external requests on this internal project, and a risk question on the level form.
        await Assert.ThrowsAsync<NotFoundException>(() => AnswerAsync(projectId, externalOnly.Id, new SaveAnswerDto { OptionId = externalOnly.Options[0].Id }, Level));
        await Assert.ThrowsAsync<NotFoundException>(() => AnswerAsync(projectId, riskQuestion.Id, new SaveAnswerDto(), Level));
    }

    [Fact]
    public async Task SaveAnswer_ClosedProject_Throws409()
    {
        var projectId = await TestProjects.CreateAsync(database, null, ProjectStatus.Closed);
        var question = (await GetAsync(projectId)).Questions[0].Question;

        await Assert.ThrowsAsync<ConflictException>(() =>
            AnswerAsync(projectId, question.Id, new SaveAnswerDto { OptionId = OptionOf(question, "no-risk").Id }));
    }

    [Fact]
    public async Task SaveAnswer_NotSignedIn_Throws403()
    {
        var projectId = await TestProjects.CreateAsync(database, "5");
        var question = (await GetAsync(projectId)).Questions[0].Question;

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            AsAsync(null, s => s.SaveAnswerAsync(projectId, Risk, question.Id, new SaveAnswerDto(), default)));
    }

    // ---- Header ----

    [Fact]
    public async Task Save_Header_IsKeptWithTheApprover()
    {
        var projectId = await TestProjects.CreateAsync(database, "5");
        var managerId = await TestProjects.UserIdAsync(database, TestProjects.Manager);

        var form = await SaveHeaderAsync(projectId, new SaveQuestionnaireDto
        {
            Subcategory = RequestSource.Internal, Participants = "Elif Yılmaz, Mert Kaya", AssessedOn = new DateOnly(2026, 11, 11),
            ApprovedByUserId = managerId, ApprovedOn = new DateOnly(2026, 11, 12),
        }, "internal-approval");

        Assert.Equal("Elif Yılmaz, Mert Kaya", form.Participants);
        Assert.Equal(new DateOnly(2026, 11, 11), form.AssessedOn);
        Assert.Equal(managerId, form.ApprovedBy!.Id);
        Assert.Equal(new DateOnly(2026, 11, 12), form.ApprovedOn);
    }

    [Fact]
    public async Task Save_UnknownApprover_Throws400()
    {
        var projectId = await TestProjects.CreateAsync(database, "5");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => SaveHeaderAsync(projectId,
            new SaveQuestionnaireDto { Subcategory = RequestSource.Internal, ApprovedByUserId = int.MaxValue }));
        Assert.Equal("Seçilen onaylayan aktif bir kullanıcı değil.", ex.Message);
    }

    [Fact]
    public async Task Save_OtherSubcategory_ChangesTheList_AndHiddenAnswersComeBack()
    {
        var projectId = await TestProjects.CreateAsync(database, "5");
        var internalQuestion = (await GetAsync(projectId, Level)).Questions[0].Question;
        await AnswerAsync(projectId, internalQuestion.Id, new SaveAnswerDto { OptionId = internalQuestion.Options[0].Id }, Level);

        var external = await SaveHeaderAsync(projectId, new SaveQuestionnaireDto { Subcategory = RequestSource.External }, Level);

        Assert.Equal(26, external.Summary.Listed);
        Assert.Equal(0, external.Summary.Answered);

        var back = await SaveHeaderAsync(projectId, new SaveQuestionnaireDto { Subcategory = RequestSource.Internal }, Level);

        Assert.Equal(24, back.Summary.Listed);
        Assert.Equal(1, back.Summary.Answered);
    }
}
