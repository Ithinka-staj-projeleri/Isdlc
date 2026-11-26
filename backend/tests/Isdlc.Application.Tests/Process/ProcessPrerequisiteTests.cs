using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Process;
using Isdlc.Application.Process.Dtos;
using Isdlc.Application.QuestionBank;
using Isdlc.Application.Tests.Common;
using Isdlc.Domain.Process;
using Isdlc.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Process;

/// <summary>
/// A step cannot be passed without its work (Faz 7 · A4), on the company's own flow: Ithinka's new software project asks
/// for a meeting at 6, a risk at 8, the level at 5 and the customer fit survey at 4. Each test creates its own project.
/// Rules: docs/configurability.md → Adım ön koşulları.
/// </summary>
[Collection("Database")]
public class ProcessPrerequisiteTests(DatabaseFixture database)
{
    private async Task<T> AsAsync<T>(string email, Func<IProcessService, Task<T>> action)
    {
        var userId = await TestProjects.UserIdAsync(database, email);
        await using var scope = database.CreateScope();
        var service = new ProcessService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
            new TestUser(userId),
            new RecordingNotificationService(),
            TimeProvider.System,
            scope.ServiceProvider.GetRequiredService<IQuestionnaireService>());
        return await action(service);
    }

    private Task<int> Step(string code) => TestProjects.StepIdAsync(database, code);

    private async Task<ProjectProcessDto> CompleteAsync(int projectId, string stepCode) =>
        await AsAsync(TestProjects.Analyst, async s =>
            await s.CompleteStepAsync(projectId, new CompleteStepDto { ExpectedStepId = await Step(stepCode) }, default));

    [Fact]
    public async Task Seed_NewSoftwareProject_AsksForTheWorkOfItsSteps()
    {
        await using var scope = database.CreateScope();
        var steps = await scope.ServiceProvider.GetRequiredService<IApplicationDbContext>().ProcessSteps
            .AsNoTracking()
            .Where(s => s.ProcessFlow.Code == ProcessFlowCodes.NewRequest)
            .Select(s => new { s.Code, s.Prerequisites })
            .ToListAsync();

        // The list is a JSON column: filtered here, not in SQL.
        var byCode = steps.Where(s => s.Prerequisites.Count > 0).ToDictionary(s => s.Code, s => Assert.Single(s.Prerequisites));
        Assert.Equal(["3", "4", "5", "6", "8"], byCode.Keys.Order());
        Assert.Equal(new StepPrerequisite(PrerequisiteKind.RequestFormComplete), byCode["3"]);
        Assert.Equal(new StepPrerequisite(PrerequisiteKind.QuestionnaireComplete, "customer-fit"), byCode["4"]);
        Assert.Equal(new StepPrerequisite(PrerequisiteKind.LevelApplied), byCode["5"]);
        Assert.Equal(new StepPrerequisite(PrerequisiteKind.MeetingExists), byCode["6"]);
        Assert.Equal(new StepPrerequisite(PrerequisiteKind.RiskExists), byCode["8"]);
    }

    [Fact]
    public async Task GetProcess_MissingMeeting_ListsItWithTheWayThereAndCannotComplete()
    {
        var projectId = await TestProjects.CreateAsync(database, "6");

        var process = await AsAsync(TestProjects.Analyst, s => s.GetProjectProcessAsync(projectId, default));

        Assert.False(process.AvailableActions.CanComplete);
        Assert.Null(process.AvailableActions.BlockedReason);
        var unmet = Assert.Single(process.AvailableActions.UnmetPrerequisites);
        Assert.Equal(PrerequisiteKind.MeetingExists, unmet.Kind);
        Assert.Equal("Tutanaklara git", unmet.LinkText);
        Assert.Equal($"/projects/{projectId}?tab=meetings", unmet.Link);
        Assert.Equal([new StepPrerequisite(PrerequisiteKind.MeetingExists)], process.CurrentStep!.Prerequisites);
    }

    [Fact]
    public async Task Complete_MissingMeeting_IsRefusedAndTheProjectStays()
    {
        var projectId = await TestProjects.CreateAsync(database, "6");

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => CompleteAsync(projectId, "6"));

        Assert.Equal("Bu adım tamamlanamaz: En az bir toplantı tutanağı girilmeli.", error.Message);
        Assert.Equal("6", (await TestProjects.GetAsync(database, projectId)).CurrentStep!.Code);
    }

    [Theory]
    [InlineData("5", PrerequisiteKind.LevelApplied, "6")]
    [InlineData("6", PrerequisiteKind.MeetingExists, "7")]
    [InlineData("8", PrerequisiteKind.RiskExists, "9")]
    public async Task Complete_AfterTheWorkIsDone_MovesOn(string stepCode, PrerequisiteKind kind, string nextCode)
    {
        var projectId = await TestProjects.CreateAsync(database, stepCode);
        await TestProjects.SatisfyAsync(database, projectId, kind);

        var process = await CompleteAsync(projectId, stepCode);

        Assert.Equal(nextCode, process.CurrentStep!.Code);
    }

    [Fact]
    public async Task Complete_RequestFormFilled_Step3MovesOn()
    {
        // TestProjects fills every required field of the request form, like the request screen does.
        var projectId = await TestProjects.CreateAsync(database, "3");

        var process = await CompleteAsync(projectId, "3");

        Assert.NotEqual("3", process.CurrentStep!.Code);
    }

    [Fact]
    public async Task Complete_RequestFormMissingARequiredField_Step3IsRefused()
    {
        var projectId = await TestProjects.CreateAsync(database, "3");
        await using (var scope = database.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var project = await db.Projects.SingleAsync(p => p.Id == projectId);
            project.RequestChannel = RequestChannel.Other;
            project.RequestChannelOther = null;
            await db.SaveChangesAsync();
        }

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => CompleteAsync(projectId, "3"));

        Assert.Equal("Bu adım tamamlanamaz: Talep Kayıt Formu'nun zorunlu alanları doldurulmalı.", error.Message);
    }

    [Fact]
    public async Task AnswerYes_SurveyNotFilled_IsRefused()
    {
        var projectId = await TestProjects.CreateAsync(database, "4", source: RequestSource.External);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => AsAsync(TestProjects.Analyst, async s =>
            await s.AnswerStepAsync(projectId, new AnswerStepDto { ExpectedStepId = await Step("4"), Answer = StepAnswer.Yes }, default)));

        Assert.StartsWith("Bu adım tamamlanamaz: ", error.Message);
        Assert.EndsWith(" tamamlanmalı.", error.Message);
    }

    [Fact]
    public async Task AnswerNo_SurveyNotFilled_StillClosesTheRequest()
    {
        // "No" is a decision about the request, not the end of the step's work: prerequisites are not asked.
        var projectId = await TestProjects.CreateAsync(database, "4", source: RequestSource.External);

        var process = await AsAsync(TestProjects.Analyst, async s => await s.AnswerStepAsync(projectId, new AnswerStepDto
        {
            ExpectedStepId = await Step("4"),
            Answer = StepAnswer.No,
            ClosingDecision = TestDecisions.Form(),
        }, default));

        Assert.Equal(ProjectStatus.Rejected, process.Status);
    }

    [Fact]
    public async Task GetProcess_DecisionStepMissingItsWork_CanStillBeAnswered()
    {
        // The screen keeps "No" and disables "Yes" while the list is not empty.
        var projectId = await TestProjects.CreateAsync(database, "4", source: RequestSource.External);

        var process = await AsAsync(TestProjects.Analyst, s => s.GetProjectProcessAsync(projectId, default));

        Assert.True(process.AvailableActions.CanAnswer);
        Assert.Equal(PrerequisiteKind.QuestionnaireComplete, Assert.Single(process.AvailableActions.UnmetPrerequisites).Kind);
    }

    [Fact]
    public async Task GetProcess_SomeoneWhoCannotActOnTheStep_SeesWhyNotTheWork()
    {
        var projectId = await TestProjects.CreateAsync(database, "6");

        var process = await AsAsync(TestProjects.Developer, s => s.GetProjectProcessAsync(projectId, default));

        Assert.NotNull(process.AvailableActions.BlockedReason);
        Assert.Empty(process.AvailableActions.UnmetPrerequisites);
    }
}
