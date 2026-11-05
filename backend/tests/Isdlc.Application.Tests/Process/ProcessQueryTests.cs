using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Common.Models;
using Isdlc.Application.Process;
using Isdlc.Application.Process.Dtos;
using Isdlc.Application.Tests.Common;
using Isdlc.Domain.Process;
using Isdlc.Domain.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Process;

/// <summary>
/// The project's process state (#19) and process history (#21), read from the seed projects.
/// These tests only read, so seed projects can be used directly.
/// Seed: Isdlc.Infrastructure/Seed/sample-projects.json.
/// </summary>
[Collection("Database")]
public class ProcessQueryTests(DatabaseFixture database)
{
    private async Task<T> AsUserAsync<T>(string? email, Func<IProcessService, Task<T>> call)
    {
        var userId = email is null ? (int?)null : await TestProjects.UserIdAsync(database, email);
        await using var scope = database.CreateScope();
        var service = new ProcessService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
            new TestUser(userId),
            new RecordingNotificationService(),
            TimeProvider.System);
        return await call(service);
    }

    private async Task<ProjectProcessDto> ProcessOfAsync(string projectCode, string? email = TestProjects.Analyst)
    {
        var projectId = await TestProjects.SeedProjectIdAsync(database, projectCode);
        return await AsUserAsync(email, s => s.GetProjectProcessAsync(projectId, default));
    }

    // ---- Project process state (#19) ----

    [Fact]
    public async Task ProjectProcess_ActiveProject_ShowsCurrentStepStagesAndProgress()
    {
        // ISDLC-2026-016 waits on 4.5 (order 13 of 75).
        var process = await ProcessOfAsync("ISDLC-2026-016");

        Assert.Equal(ProjectStatus.Active, process.Status);
        Assert.Equal("4.5", process.CurrentStep!.Code);
        Assert.Equal(16, process.ProgressPercent); // 12 * 100 / 75 = 16

        Assert.Equal(13, process.Stages.Count);
        Assert.All(process.Stages.Where(s => s.Order < 4), s => Assert.Equal(ProcessStageState.Completed, s.State));
        Assert.Equal(ProcessStageState.Current, process.Stages.Single(s => s.Order == 4).State);
        Assert.All(process.Stages.Where(s => s.Order > 4), s => Assert.Equal(ProcessStageState.Pending, s.State));
    }

    [Fact]
    public async Task ProjectProcess_StageStepCounts_AddUpToTheWholeFlow()
    {
        var process = await ProcessOfAsync("ISDLC-2026-016");

        Assert.Equal(75, process.Stages.Sum(s => s.StepCount));
    }

    [Fact]
    public async Task ProjectProcess_ApprovalStep_AnalystIsBlockedWithTheReason()
    {
        var actions = (await ProcessOfAsync("ISDLC-2026-016", TestProjects.Analyst)).AvailableActions;

        Assert.False(actions.CanAnswer);
        Assert.False(actions.CanReject);
        Assert.Equal("Bu adım Yazılım Departman Yöneticisi tarafından onaylanır.", actions.BlockedReason);
    }

    [Fact]
    public async Task ProjectProcess_ApprovalStep_ManagerCanAnswerAndReject()
    {
        var actions = (await ProcessOfAsync("ISDLC-2026-016", TestProjects.Manager)).AvailableActions;

        Assert.Equal(new AvailableActionsDto(CanComplete: false, CanAnswer: true, CanReject: true, BlockedReason: null), actions);
    }

    [Fact]
    public async Task ProjectProcess_NobodySignedIn_NothingAllowed()
    {
        var actions = (await ProcessOfAsync("ISDLC-2026-016", email: null)).AvailableActions;

        Assert.Equal("İşlem yapmak için giriş yapılmalıdır.", actions.BlockedReason);
        Assert.False(actions.CanComplete || actions.CanAnswer || actions.CanReject);
    }

    [Fact]
    public async Task ProjectProcess_ClosedProject_IsComplete()
    {
        var process = await ProcessOfAsync("ISDLC-2026-009");

        Assert.Equal(ProjectStatus.Closed, process.Status);
        Assert.Null(process.CurrentStep);
        Assert.Equal(100, process.ProgressPercent);
        Assert.All(process.Stages, s => Assert.Equal(ProcessStageState.Completed, s.State));
        Assert.Equal("Proje aktif değil; süreçte işlem yapılamaz.", process.AvailableActions.BlockedReason);
    }

    [Fact]
    public async Task ProjectProcess_LostProject_StageWhereItStoppedIsStopped()
    {
        // ISDLC-2026-011 was lost at 5.3 (order 20, stage 5).
        var process = await ProcessOfAsync("ISDLC-2026-011");

        Assert.Equal(ProjectStatus.Lost, process.Status);
        Assert.Equal(25, process.ProgressPercent); // 19 * 100 / 75 = 25
        Assert.Equal(ProcessStageState.Completed, process.Stages.Single(s => s.Order == 4).State);
        Assert.Equal(ProcessStageState.Stopped, process.Stages.Single(s => s.Order == 5).State);
        Assert.Equal(ProcessStageState.Pending, process.Stages.Single(s => s.Order == 6).State);
    }

    [Fact]
    public async Task ProjectProcess_ProjectNotFound_Is404()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            AsUserAsync(TestProjects.Analyst, s => s.GetProjectProcessAsync(999_999, default)));
    }

    // ---- Process history (#21) ----

    [Fact]
    public async Task StepRecords_AreNewestFirstAndPaged()
    {
        // ISDLC-2026-014 waits on 7.4 and has one record for each of the 29 steps before it.
        var projectId = await TestProjects.SeedProjectIdAsync(database, "ISDLC-2026-014");

        var first = await AsUserAsync(TestProjects.Analyst,
            s => s.GetStepRecordsAsync(projectId, new PageRequest { Page = 1, PageSize = 10 }, default));
        var last = await AsUserAsync(TestProjects.Analyst,
            s => s.GetStepRecordsAsync(projectId, new PageRequest { Page = 3, PageSize = 10 }, default));

        Assert.Equal(29, first.TotalCount);
        Assert.Equal(10, first.Items.Count);
        Assert.Equal(9, last.Items.Count);

        Assert.Equal("7.3", first.Items[0].StepCode);   // the step just before 7.4
        Assert.Equal("1.1", last.Items[^1].StepCode);   // the very first step
        Assert.Equal(first.Items.Select(r => r.PerformedAt).OrderDescending(), first.Items.Select(r => r.PerformedAt));
    }

    [Fact]
    public async Task StepRecords_ShowWhoDidWhatAndWhereTheFlowWentBack()
    {
        var projectId = await TestProjects.CreateAsync(database, "4.7");
        var analystId = await TestProjects.UserIdAsync(database, TestProjects.Analyst);

        await AsUserAsync(TestProjects.Analyst, async s => await s.AnswerStepAsync(projectId, new AnswerStepDto
        {
            ExpectedStepId = await TestProjects.StepIdAsync(database, "4.7"),
            Answer = StepAnswer.No,
            JumpTargetStepId = await TestProjects.StepIdAsync(database, "4.1"),
            Note = "Müşteri iki ekranda düzeltme istedi.",
        }, default));

        var history = await AsUserAsync(TestProjects.Analyst,
            s => s.GetStepRecordsAsync(projectId, new PageRequest(), default));

        var record = Assert.Single(history.Items);
        Assert.Equal("4.7", record.StepCode);
        Assert.Equal(StepAction.No, record.Action);
        Assert.Equal("4.1", record.JumpedToStepCode);
        Assert.Equal("Müşteri iki ekranda düzeltme istedi.", record.Note);
        Assert.Equal(new UserSummaryDto(analystId, "Elif Yılmaz"), record.PerformedBy);
    }

    [Fact]
    public async Task StepRecords_ProjectNotFound_Is404()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            AsUserAsync(TestProjects.Analyst, s => s.GetStepRecordsAsync(999_999, new PageRequest(), default)));
    }
}
