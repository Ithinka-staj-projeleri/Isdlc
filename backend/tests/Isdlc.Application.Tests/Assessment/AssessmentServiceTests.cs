using Isdlc.Application.Assessment;
using Isdlc.Application.Assessment.Dtos;
using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.QuestionBank;
using Isdlc.Application.QuestionBank.Dtos;
using Isdlc.Application.Tests.Common;
using Isdlc.Domain.Assessment;
using Isdlc.Domain.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Assessment;

/// <summary>
/// Seviye &amp; Uygunluk on a real database with the seeded forms: the forms' results and applying the level.
/// Every test creates its own project, so seed projects are never changed. Expectations: docs/assessment-api.md.
/// </summary>
[Collection("Database")]
public class AssessmentServiceTests(DatabaseFixture database)
{
    private async Task<T> AsAsync<T>(string? email, Func<IAssessmentService, IQuestionnaireService, Task<T>> action)
    {
        var userId = email is null ? (int?)null : await TestProjects.UserIdAsync(database, email);
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var user = new TestUser(userId);
        var questionnaires = new QuestionnaireService(db, user);
        return await action(new AssessmentService(db, user, questionnaires), questionnaires);
    }

    private Task<QuestionnaireDto> FormAsync(int projectId, string setCode) =>
        AsAsync(TestProjects.Analyst, (_, q) => q.GetAsync(projectId, setCode, default));

    private Task<QuestionnaireDto> ApplyAsync(int projectId, string? email = TestProjects.Analyst) =>
        AsAsync(email, (a, _) => a.ApplyLevelAsync(projectId, default));

    private Task<int> AnswerLevelAsync(int projectId, Func<Domain.QuestionBank.Question, Domain.QuestionBank.QuestionOption?> choose,
        string? section = null) =>
        TestQuestionnaires.AnswerAsync(database, projectId, LevelRules.SetCode, RequestSource.Internal, choose, section);

    // ---- Level form result ----

    [Fact]
    public async Task LevelForm_NothingAnswered_ShowsZeroAndNoResult()
    {
        var projectId = await TestProjects.CreateAsync(database, "5");

        var result = Assert.IsType<LevelResultDto>((await FormAsync(projectId, LevelRules.SetCode)).Result);

        // Internal request: 12 questions in each section (the 13th is for external requests), 60 points at most.
        Assert.Equal(new DocumentLevelResultDto(0, 60, 0, 12, null), result.DocumentLevel);
        Assert.Equal(new ResearchResultDto(0, 60, 0, 12, null), result.Research);
        Assert.Null(result.AppliedLevel);
    }

    [Fact]
    public async Task LevelForm_AllHighest_IsLevelThreeAndThirtyDays()
    {
        var projectId = await TestProjects.CreateAsync(database, "5");
        await AnswerLevelAsync(projectId, TestQuestionnaires.Highest);

        var result = Assert.IsType<LevelResultDto>((await FormAsync(projectId, LevelRules.SetCode)).Result);

        Assert.Equal((60, 3), (result.DocumentLevel.Score, result.DocumentLevel.Level));
        Assert.Equal((60, 30), (result.Research.Score, result.Research.Days));
    }

    [Fact]
    public async Task LevelForm_OnlyFirstSectionAnswered_HasLevelButNoDays()
    {
        var projectId = await TestProjects.CreateAsync(database, "5");
        await AnswerLevelAsync(projectId, TestQuestionnaires.Lowest, LevelRules.DocumentLevelSection);

        var result = Assert.IsType<LevelResultDto>((await FormAsync(projectId, LevelRules.SetCode)).Result);

        Assert.Equal(1, result.DocumentLevel.Level);
        Assert.Null(result.Research.Days);
    }

    // ---- Customer fit survey result ----

    [Fact]
    public async Task CustomerFit_ExternalRequestAllAnswered_GivesTheRating()
    {
        var projectId = await TestProjects.CreateAsync(database, "4", source: RequestSource.External);
        var points = new Queue<int>([8, 8, 8, 7, 7, 7, 7, 7, 8, 8]); // 75
        await TestQuestionnaires.AnswerAsync(database, projectId, CustomerFitRules.SetCode, RequestSource.External,
            q => TestQuestionnaires.Points(q, points.Dequeue()));

        var form = await FormAsync(projectId, CustomerFitRules.SetCode);

        Assert.Equal(new CustomerFitResultDto(75, 100, 10, 10, CustomerFitRating.ConditionallyFit), form.Result);
    }

    [Fact]
    public async Task CustomerFit_InternalRequest_ListsNoQuestionsAndHasNoRating()
    {
        var projectId = await TestProjects.CreateAsync(database, "4");

        var form = await FormAsync(projectId, CustomerFitRules.SetCode);

        Assert.Empty(form.Questions);
        Assert.Equal(new CustomerFitResultDto(0, 0, 0, 0, null), form.Result);
    }

    // ---- Apply level ----

    [Fact]
    public async Task ApplyLevel_BothSectionsAnswered_WritesLevelAndResearchDays()
    {
        var projectId = await TestProjects.CreateAsync(database, "5");
        await AnswerLevelAsync(projectId, TestQuestionnaires.Highest, LevelRules.DocumentLevelSection);
        await AnswerLevelAsync(projectId, TestQuestionnaires.Lowest, LevelRules.ResearchSection);

        var form = await ApplyAsync(projectId);

        var result = Assert.IsType<LevelResultDto>(form.Result);
        var project = await TestProjects.GetAsync(database, projectId);
        Assert.Equal((3, (int?)result.Research.Days), (project.Level, project.ResearchDays));
        Assert.Equal((3, result.Research.Days), (result.AppliedLevel, result.AppliedResearchDays));
        Assert.NotNull(project.ResearchDays);
    }

    [Fact]
    public async Task ApplyLevel_AnswersChangeLater_ProjectKeepsTheAppliedLevel()
    {
        var projectId = await TestProjects.CreateAsync(database, "5");
        await AnswerLevelAsync(projectId, TestQuestionnaires.Lowest);
        await ApplyAsync(projectId);

        // A later answer does not change the project; the form shows the difference.
        await AsAsync(TestProjects.Analyst, async (_, q) =>
        {
            var form = await q.GetAsync(projectId, LevelRules.SetCode, default);
            foreach (var item in form.Questions.Where(i => i.Question.Section == LevelRules.DocumentLevelSection))
            {
                var highest = item.Question.Options.MaxBy(o => o.Points ?? 0)!;
                await q.SaveAnswerAsync(projectId, LevelRules.SetCode, item.Question.Id, new SaveAnswerDto { OptionId = highest.Id }, default);
            }
            return 0;
        });

        var result = Assert.IsType<LevelResultDto>((await FormAsync(projectId, LevelRules.SetCode)).Result);
        Assert.Equal((3, 1), (result.DocumentLevel.Level, result.AppliedLevel));
        Assert.Equal(1, (await TestProjects.GetAsync(database, projectId)).Level);
    }

    [Fact]
    public async Task ApplyLevel_SectionNotComplete_ThrowsBusinessRule()
    {
        var projectId = await TestProjects.CreateAsync(database, "5");
        await AnswerLevelAsync(projectId, TestQuestionnaires.Highest, LevelRules.DocumentLevelSection);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => ApplyAsync(projectId));

        Assert.Equal("Seviyeyi uygulamak için bütün sorular cevaplanmalıdır.", error.Message);
        Assert.Null((await TestProjects.GetAsync(database, projectId)).Level);
    }

    [Fact]
    public async Task ApplyLevel_ClosedProject_ThrowsConflict()
    {
        var projectId = await TestProjects.CreateAsync(database, null, ProjectStatus.Closed);

        await Assert.ThrowsAsync<ConflictException>(() => ApplyAsync(projectId));
    }

    [Fact]
    public async Task ApplyLevel_UnknownProject_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => ApplyAsync(int.MaxValue));
    }

    [Fact]
    public async Task ApplyLevel_NotSignedIn_ThrowsForbidden()
    {
        var projectId = await TestProjects.CreateAsync(database, "5");

        await Assert.ThrowsAsync<ForbiddenException>(() => ApplyAsync(projectId, email: null));
    }
}
