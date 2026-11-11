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

    // ---- Flows (Faz 4) ----

    [Fact]
    public async Task Flows_ListTheNewRequestFlowWithItsStepCount()
    {
        var flows = await AsUserAsync(TestProjects.Analyst, s => s.GetFlowsAsync(default));

        var flow = Assert.Single(flows, f => f.Code == ProcessFlowCodes.NewRequest);
        Assert.Equal(ProcessFlowKind.Request, flow.Kind);
        Assert.Equal(3, flow.ProjectStageOrder);
        Assert.Equal(19, flow.StepCount);
    }

    [Fact]
    public async Task Flow_UnknownCode_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => AsUserAsync(TestProjects.Analyst, s => s.GetFlowAsync("no-such-flow", default)));
    }

    [Fact]
    public async Task ProjectProcess_ExternalRequestWithAnOffer_SkipsNothing()
    {
        var process = await ProcessOfAsync("ISDLC-2026-016");

        Assert.Equal(new ProcessFlowSummaryDto(ProcessFlowCodes.NewRequest, "Yeni Yazılım Projesi"), process.Flow);
        Assert.Empty(process.SkippedStepIds);
    }

    [Fact]
    public async Task ProjectProcess_InternalRequestWithoutOffer_SkipsSuitabilityAndOfferSteps()
    {
        // ISDLC-2026-015: internal, no offer ("doğrudan proje"), waiting on 16.
        var process = await ProcessOfAsync("ISDLC-2026-015");

        Assert.Equal(
            [await TestProjects.StepIdAsync(database, "4"), await TestProjects.StepIdAsync(database, "10"),
             await TestProjects.StepIdAsync(database, "11")],
            process.SkippedStepIds);
        // 12 of the 16 steps done on this project come before 16: 12 * 100 / 16 = 75.
        Assert.Equal(75, process.ProgressPercent);
    }

    // ---- Project process state (#19) ----

    [Fact]
    public async Task ProjectProcess_ActiveProject_ShowsCurrentStepStagesAndProgress()
    {
        // ISDLC-2026-016 waits on 10 (stage 2); every one of the 19 steps is done on it.
        var process = await ProcessOfAsync("ISDLC-2026-016");

        Assert.Equal(ProjectStatus.Active, process.Status);
        Assert.Equal("10", process.CurrentStep!.Code);
        Assert.Equal(47, process.ProgressPercent); // 9 * 100 / 19 = 47

        Assert.Equal(4, process.Stages.Count);
        Assert.Equal(ProcessStageState.Completed, process.Stages.Single(s => s.Order == 1).State);
        Assert.Equal(ProcessStageState.Current, process.Stages.Single(s => s.Order == 2).State);
        Assert.All(process.Stages.Where(s => s.Order > 2), s => Assert.Equal(ProcessStageState.Pending, s.State));
    }

    [Fact]
    public async Task ProjectProcess_StageStepCounts_AddUpToTheWholeFlow()
    {
        var process = await ProcessOfAsync("ISDLC-2026-016");

        Assert.Equal(19, process.Stages.Sum(s => s.StepCount));
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
        // ISDLC-2026-011 was lost at 11 (stage 2): the customer did not accept the offer.
        var process = await ProcessOfAsync("ISDLC-2026-011");

        Assert.Equal(ProjectStatus.Lost, process.Status);
        Assert.Equal(52, process.ProgressPercent); // 10 * 100 / 19 = 52
        Assert.Equal(ProcessStageState.Completed, process.Stages.Single(s => s.Order == 1).State);
        Assert.Equal(ProcessStageState.Stopped, process.Stages.Single(s => s.Order == 2).State);
        Assert.Equal(ProcessStageState.Pending, process.Stages.Single(s => s.Order == 3).State);
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
        // ISDLC-2026-014 waits on 14 and has one record for each of the 13 steps before it.
        var projectId = await TestProjects.SeedProjectIdAsync(database, "ISDLC-2026-014");

        var first = await AsUserAsync(TestProjects.Analyst,
            s => s.GetStepRecordsAsync(projectId, new PageRequest { Page = 1, PageSize = 10 }, default));
        var last = await AsUserAsync(TestProjects.Analyst,
            s => s.GetStepRecordsAsync(projectId, new PageRequest { Page = 2, PageSize = 10 }, default));

        Assert.Equal(13, first.TotalCount);
        Assert.Equal(10, first.Items.Count);
        Assert.Equal(3, last.Items.Count);

        Assert.Equal("13", first.Items[0].StepCode);   // the step just before 14
        Assert.Equal("1", last.Items[^1].StepCode);    // the very first step
        Assert.Equal(first.Items.Select(r => r.PerformedAt).OrderDescending(), first.Items.Select(r => r.PerformedAt));
    }

    [Fact]
    public async Task StepRecords_ShowWhoDidWhatAndWhereTheFlowWentBack()
    {
        // Go-live (17) is done by a developer; "No" returns to testing (16).
        var projectId = await TestProjects.CreateAsync(database, "17");
        var developerId = await TestProjects.UserIdAsync(database, TestProjects.Developer);

        await AsUserAsync(TestProjects.Developer, async s => await s.AnswerStepAsync(projectId, new AnswerStepDto
        {
            ExpectedStepId = await TestProjects.StepIdAsync(database, "17"),
            Answer = StepAnswer.No,
            JumpTargetStepId = await TestProjects.StepIdAsync(database, "16"),
            Note = "Canlıda iki ekranda hata çıktı.",
        }, default));

        var history = await AsUserAsync(TestProjects.Analyst,
            s => s.GetStepRecordsAsync(projectId, new PageRequest(), default));

        var record = Assert.Single(history.Items);
        Assert.Equal("17", record.StepCode);
        Assert.Equal(StepAction.No, record.Action);
        Assert.Equal("16", record.JumpedToStepCode);
        Assert.Equal("Canlıda iki ekranda hata çıktı.", record.Note);
        Assert.Equal(developerId, record.PerformedBy.Id);
    }

    [Fact]
    public async Task StepRecords_ProjectNotFound_Is404()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            AsUserAsync(TestProjects.Analyst, s => s.GetStepRecordsAsync(999_999, new PageRequest(), default)));
    }
}
