using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Common.Models;
using Isdlc.Application.Process;
using Isdlc.Application.Process.Dtos;
using Isdlc.Application.Projects;
using Isdlc.Application.Projects.Dtos;
using Isdlc.Application.Tests.Common;
using Isdlc.Domain.Process;
using Isdlc.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Process;

/// <summary>
/// Talep Ret / Erteleme Formu (D2): reject, postpone and take back a request, and the "No" answers that close one.
/// Every test creates its own project (TestProjects), so seed projects are never changed.
/// Expectations: docs/process-api.md → "Ret / erteleme kararı".
/// </summary>
[Collection("Database")]
public class RequestDecisionTests(DatabaseFixture database)
{
    private sealed class Session(AsyncServiceScope scope, IProcessService service, RecordingNotificationService notifications)
        : IAsyncDisposable
    {
        public IProcessService Service { get; } = service;
        public RecordingNotificationService Notifications { get; } = notifications;
        public ValueTask DisposeAsync() => scope.DisposeAsync();
    }

    private async Task<Session> SignInAsync(string email)
    {
        var userId = await TestProjects.UserIdAsync(database, email);
        var scope = database.CreateScope();
        var notifications = new RecordingNotificationService();
        var service = new ProcessService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
            new TestUser(userId),
            notifications,
            TimeProvider.System);
        return new Session(scope, service, notifications);
    }

    private Task<int> Step(string code, string flowCode = ProcessFlowCodes.NewRequest) =>
        TestProjects.StepIdAsync(database, code, flowCode);

    private async Task<List<StepRecord>> RecordsAsync(int projectId)
    {
        await using var scope = database.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IApplicationDbContext>().StepRecords
            .AsNoTracking()
            .Include(r => r.ProcessStep)
            .Where(r => r.ProjectId == projectId)
            .OrderBy(r => r.Id)
            .ToListAsync();
    }

    private async Task<IReadOnlyList<RequestDecisionDto>> DecisionsAsync(int projectId)
    {
        await using var scope = database.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IProcessService>()
            .GetRequestDecisionsAsync(projectId, CancellationToken.None);
    }

    private async Task<ProjectProcessDto> PostponeAsync(int projectId, string stepCode, DateOnly? reevaluation = null)
    {
        await using var manager = await SignInAsync(TestProjects.Manager);
        return await manager.Service.PostponeAsync(projectId, new PostponeProjectDto
        {
            ExpectedStepId = await Step(stepCode),
            Decision = TestDecisions.Form(),
            ReevaluationDate = reevaluation ?? new DateOnly(2027, 1, 15),
        }, default);
    }

    // ---- Reject ----

    [Fact]
    public async Task Reject_SavesTheFormWithTheRecord()
    {
        var projectId = await TestProjects.CreateAsync(database, "5");
        await using var manager = await SignInAsync(TestProjects.Manager);

        await manager.Service.RejectAsync(projectId, new RejectProjectDto
        {
            ExpectedStepId = await Step("5"),
            Decision = TestDecisions.Form("  Stratejiye uymuyor.  ") with
            {
                DecidedBy = "  Proje Kurulu  ",
                // A repeated reason counts once; the "Diğer" text is kept with Other.
                Reasons = [DecisionReason.StrategicMisfit, DecisionReason.Other, DecisionReason.StrategicMisfit],
                ReasonOther = "  Lisans sorunu  ",
            },
        }, default);

        var decision = Assert.Single(await DecisionsAsync(projectId));
        Assert.Equal(RequestDecisionKind.Reject, decision.Decision);
        Assert.Equal(TestDecisions.DecisionDate, decision.DecisionDate);
        Assert.Equal("Proje Kurulu", decision.DecidedBy);
        Assert.Equal([DecisionReason.StrategicMisfit, DecisionReason.Other], decision.Reasons);
        Assert.Equal("Lisans sorunu", decision.ReasonOther);
        Assert.Equal("Stratejiye uymuyor.", decision.Explanation);
        Assert.Null(decision.ReevaluationDate);
        Assert.Equal("5", decision.StepCode);
        Assert.Equal("Stratejiye uymuyor.", Assert.Single(await RecordsAsync(projectId)).Note);
    }

    [Fact]
    public async Task Reject_OtherTextWithoutOther_IsNotKept()
    {
        var projectId = await TestProjects.CreateAsync(database, "5");
        await using var manager = await SignInAsync(TestProjects.Manager);

        await manager.Service.RejectAsync(projectId, new RejectProjectDto
        {
            ExpectedStepId = await Step("5"),
            Decision = TestDecisions.Form() with { ReasonOther = "Kullanılmayacak" },
        }, default);

        Assert.Null(Assert.Single(await DecisionsAsync(projectId)).ReasonOther);
    }

    // ---- Postpone ----

    [Fact]
    public async Task Postpone_Manager_WaitsOnTheSameStepAsPostponed()
    {
        var projectId = await TestProjects.CreateAsync(database, "3");
        var code = (await TestProjects.GetAsync(database, projectId)).Code;
        await using var manager = await SignInAsync(TestProjects.Manager);

        var result = await manager.Service.PostponeAsync(projectId, new PostponeProjectDto
        {
            ExpectedStepId = await Step("3"),
            Decision = TestDecisions.Form(),
            ReevaluationDate = new DateOnly(2027, 1, 15),
        }, default);

        Assert.Equal(ProjectStatus.Postponed, result.Status);
        Assert.Equal("3", result.CurrentStep?.Code);

        var record = Assert.Single(await RecordsAsync(projectId));
        Assert.Equal(StepAction.Postponed, record.Action);
        Assert.Equal("3", record.ProcessStep.Code);

        var decision = Assert.Single(await DecisionsAsync(projectId));
        Assert.Equal(RequestDecisionKind.Postpone, decision.Decision);
        Assert.Equal(new DateOnly(2027, 1, 15), decision.ReevaluationDate);

        Assert.Equal(
            $"{code} ertelendi; yeniden değerlendirme tarihi 15.01.2027.",
            Assert.Single(manager.Notifications.Sent).Message);
    }

    [Fact]
    public async Task Postpone_ProjectPastTheRequestStages_Throws400()
    {
        // 12 is in stage 3: the record is a project now, so it can be rejected but not postponed.
        var projectId = await TestProjects.CreateAsync(database, "12");

        var error = await Assert.ThrowsAsync<BusinessRuleException>(async () => await PostponeAsync(projectId, "12"));

        Assert.Equal("Yalnızca talep aşamasındaki bir kayıt ertelenebilir; projeye dönüşmüş kayıt reddedilebilir.", error.Message);
        Assert.Equal(ProjectStatus.Active, (await TestProjects.GetAsync(database, projectId)).Status);
    }

    [Theory]
    [InlineData(TestProjects.Analyst)]
    [InlineData(TestProjects.ProjectManager)]
    public async Task Postpone_OtherRole_IsForbidden(string email)
    {
        var projectId = await TestProjects.CreateAsync(database, "3");
        await using var user = await SignInAsync(email);

        var error = await Assert.ThrowsAsync<ForbiddenException>(async () => await user.Service.PostponeAsync(projectId,
            new PostponeProjectDto { ExpectedStepId = await Step("3"), Decision = TestDecisions.Form(), ReevaluationDate = new DateOnly(2027, 1, 15) },
            default));

        Assert.Equal("Talebi yalnızca Yazılım Departman Yöneticisi erteleyebilir ya da değerlendirmeye geri alabilir.", error.Message);
    }

    [Fact]
    public async Task Postponed_StepCommands_Are409UntilTakenBack()
    {
        var projectId = await TestProjects.CreateAsync(database, "3");
        await PostponeAsync(projectId, "3");
        await using var analyst = await SignInAsync(TestProjects.Analyst);

        var error = await Assert.ThrowsAsync<ConflictException>(async () => await analyst.Service.CompleteStepAsync(
            projectId, new CompleteStepDto { ExpectedStepId = await Step("3") }, default));

        Assert.Equal("Talep ertelendi; önce değerlendirmeye geri alınmalıdır.", error.Message);
    }

    [Fact]
    public async Task Postponed_Process_ShowsTheStepAndOnlyResumeForTheManager()
    {
        var projectId = await TestProjects.CreateAsync(database, "3");
        await PostponeAsync(projectId, "3");
        await using var manager = await SignInAsync(TestProjects.Manager);
        await using var analyst = await SignInAsync(TestProjects.Analyst);

        var forManager = await manager.Service.GetProjectProcessAsync(projectId, default);
        var forAnalyst = await analyst.Service.GetProjectProcessAsync(projectId, default);

        Assert.Equal("3", forManager.CurrentStep?.Code);
        Assert.True(forManager.AvailableActions.CanResume);
        Assert.False(forManager.AvailableActions.CanReject || forManager.AvailableActions.CanPostpone);
        Assert.False(forAnalyst.AvailableActions.CanResume || forAnalyst.AvailableActions.CanComplete);
        Assert.Equal(
            "Talep ertelendi; Yazılım Departman Yöneticisi değerlendirmeye geri alınca devam eder.",
            forAnalyst.AvailableActions.BlockedReason);
    }

    [Fact]
    public async Task Postponed_IsListedAmongTheRequests()
    {
        var projectId = await TestProjects.CreateAsync(database, "3");
        await PostponeAsync(projectId, "3");
        await using var scope = database.CreateScope();
        var projects = scope.ServiceProvider.GetRequiredService<IProjectService>();
        var all = new PageRequest { Page = 1, PageSize = 100 };

        var requests = await projects.ListAsync(
            new ProjectFilter { Phase = ProjectPhase.Request, Status = ProjectStatus.Postponed }, all, default);
        var projectList = await projects.ListAsync(new ProjectFilter { Phase = ProjectPhase.Project }, all, default);

        Assert.Contains(requests.Items, p => p.Id == projectId);
        Assert.DoesNotContain(projectList.Items, p => p.Id == projectId);
    }

    // ---- Resume ----

    [Fact]
    public async Task Resume_Manager_ActiveAgainOnTheSameStep()
    {
        var projectId = await TestProjects.CreateAsync(database, "3");
        var code = (await TestProjects.GetAsync(database, projectId)).Code;
        await PostponeAsync(projectId, "3");
        await using var manager = await SignInAsync(TestProjects.Manager);

        var result = await manager.Service.ResumeAsync(
            projectId, new ResumeProjectDto { ExpectedStepId = await Step("3"), Note = "  Kapasite açıldı.  " }, default);

        Assert.Equal(ProjectStatus.Active, result.Status);
        Assert.Equal("3", result.CurrentStep?.Code);

        var record = (await RecordsAsync(projectId)).Last();
        Assert.Equal(StepAction.Resumed, record.Action);
        Assert.Equal("Kapasite açıldı.", record.Note);
        Assert.Equal(
            $"{code} yeniden değerlendirmeye alındı; 3. adımdan devam ediyor.",
            Assert.Single(manager.Notifications.Sent).Message);
    }

    [Fact]
    public async Task Resume_ActiveRequest_Is409()
    {
        var projectId = await TestProjects.CreateAsync(database, "3");
        await using var manager = await SignInAsync(TestProjects.Manager);

        var error = await Assert.ThrowsAsync<ConflictException>(async () => await manager.Service.ResumeAsync(
            projectId, new ResumeProjectDto { ExpectedStepId = await Step("3") }, default));

        Assert.Equal("Talep ertelenmiş değil; değerlendirmeye geri alınacak bir şey yok.", error.Message);
    }

    [Fact]
    public async Task Resume_OtherRole_IsForbidden()
    {
        var projectId = await TestProjects.CreateAsync(database, "3");
        await PostponeAsync(projectId, "3");
        await using var analyst = await SignInAsync(TestProjects.Analyst);

        await Assert.ThrowsAsync<ForbiddenException>(async () => await analyst.Service.ResumeAsync(
            projectId, new ResumeProjectDto { ExpectedStepId = await Step("3") }, default));

        Assert.Equal(ProjectStatus.Postponed, (await TestProjects.GetAsync(database, projectId)).Status);
    }

    // ---- "No" answers that close a request ----

    [Theory]
    [InlineData("4", TestProjects.Analyst)]
    [InlineData("11", TestProjects.Sales)]
    public async Task AnswerNo_ClosingRequestWithoutTheForm_Throws400(string stepCode, string answeredBy)
    {
        var projectId = await TestProjects.CreateAsync(database, stepCode, source: RequestSource.External);
        await using var user = await SignInAsync(answeredBy);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(async () => await user.Service.AnswerStepAsync(projectId,
            new AnswerStepDto { ExpectedStepId = await Step(stepCode), Answer = StepAnswer.No }, default));

        Assert.Equal(
            "Talep kapanacağı için Talep Ret / Erteleme Formu doldurulmalıdır (karar tarihi, kararı veren, gerekçe).",
            error.Message);
        Assert.Equal(ProjectStatus.Active, (await TestProjects.GetAsync(database, projectId)).Status);
    }

    [Fact]
    public async Task AnswerNo_OfferNotAccepted_SavesTheFormAsARejection()
    {
        var projectId = await TestProjects.CreateAsync(database, "11", source: RequestSource.External);
        await using var sales = await SignInAsync(TestProjects.Sales);

        await sales.Service.AnswerStepAsync(projectId, new AnswerStepDto
        {
            ExpectedStepId = await Step("11"),
            Answer = StepAnswer.No,
            ClosingDecision = TestDecisions.Form("Müşteri teklifi bütçe dışında buldu.") with
            {
                Reasons = [DecisionReason.BudgetConstraint],
            },
        }, default);

        var decision = Assert.Single(await DecisionsAsync(projectId));
        Assert.Equal(RequestDecisionKind.Reject, decision.Decision);
        Assert.Equal([DecisionReason.BudgetConstraint], decision.Reasons);
        Assert.Equal("11", decision.StepCode);
        Assert.Equal(ProjectStatus.Lost, (await TestProjects.GetAsync(database, projectId)).Status);
    }

    [Fact]
    public async Task AnswerNo_ProcessImprovementClosing_NeedsNoForm()
    {
        // The form is for requests; a rejected improvement proposal closes without it.
        var projectId = await TestProjects.CreateAsync(database, "2", flowCode: ProcessFlowCodes.ProcessImprovement);
        await using var manager = await SignInAsync(TestProjects.Manager);

        var result = await manager.Service.AnswerStepAsync(projectId, new AnswerStepDto
        {
            ExpectedStepId = await Step("2", ProcessFlowCodes.ProcessImprovement),
            Answer = StepAnswer.No,
        }, default);

        Assert.Equal(ProjectStatus.Rejected, result.Status);
        Assert.Empty(await DecisionsAsync(projectId));
    }

    // ---- Reading ----

    [Fact]
    public async Task Decisions_NewestFirst_AcrossPostponeAndReject()
    {
        var projectId = await TestProjects.CreateAsync(database, "3");
        await PostponeAsync(projectId, "3");
        await using var manager = await SignInAsync(TestProjects.Manager);
        await manager.Service.ResumeAsync(projectId, new ResumeProjectDto { ExpectedStepId = await Step("3") }, default);
        await manager.Service.RejectAsync(projectId,
            new RejectProjectDto { ExpectedStepId = await Step("3"), Decision = TestDecisions.Form() }, default);

        var decisions = await DecisionsAsync(projectId);

        Assert.Equal([RequestDecisionKind.Reject, RequestDecisionKind.Postpone], decisions.Select(d => d.Decision));
    }

    [Fact]
    public async Task Decisions_UnknownProject_Is404()
    {
        await Assert.ThrowsAsync<NotFoundException>(async () => await DecisionsAsync(int.MaxValue));
    }

    [Fact]
    public async Task SeedProjects_RejectedLostAndPostponed_HaveTheirForms()
    {
        var rejected = await DecisionsAsync(await TestProjects.SeedProjectIdAsync(database, "12"));
        var lost = await DecisionsAsync(await TestProjects.SeedProjectIdAsync(database, "11"));
        var postponed = await DecisionsAsync(await TestProjects.SeedProjectIdAsync(database, "19"));

        Assert.Equal(RequestDecisionKind.Reject, Assert.Single(rejected).Decision);
        Assert.Equal(RequestDecisionKind.Reject, Assert.Single(lost).Decision);
        Assert.Equal(RequestDecisionKind.Postpone, Assert.Single(postponed).Decision);
        Assert.Equal(ProjectStatus.Postponed, (await TestProjects.GetAsync(database, await TestProjects.SeedProjectIdAsync(database, "19"))).Status);
    }
}
