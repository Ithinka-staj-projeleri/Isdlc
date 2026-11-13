using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Process;
using Isdlc.Application.Process.Dtos;
using Isdlc.Application.Tests.Common;
using Isdlc.Domain.Identity;
using Isdlc.Domain.Process;
using Isdlc.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Process;

/// <summary>
/// Complete, answer and reject on a real database, on the company's flows (Seed/flows/*.json).
/// Every test creates its own project (TestProjects), so seed projects are never changed.
/// Expectations: docs/process-api.md and docs/data-model.md.
/// </summary>
[Collection("Database")]
public class ProcessCommandTests(DatabaseFixture database)
{
    /// <summary>The service as one user would call it, with notifications kept for checking.</summary>
    private sealed class Session(AsyncServiceScope scope, IProcessService service, RecordingNotificationService notifications)
        : IAsyncDisposable
    {
        public IProcessService Service { get; } = service;
        public RecordingNotificationService Notifications { get; } = notifications;
        public IApplicationDbContext Db => scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        public ValueTask DisposeAsync() => scope.DisposeAsync();
    }

    private async Task<Session> SignInAsync(string? email)
    {
        var userId = email is null ? (int?)null : await TestProjects.UserIdAsync(database, email);
        var scope = database.CreateScope();
        var notifications = new RecordingNotificationService();
        var service = new ProcessService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
            new TestUser(userId),
            notifications,
            TimeProvider.System);
        return new Session(scope, service, notifications);
    }

    private async Task<List<StepRecord>> RecordsAsync(int projectId)
    {
        await using var scope = database.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IApplicationDbContext>().StepRecords
            .AsNoTracking()
            .Include(r => r.ProcessStep)
            .Include(r => r.JumpedToStep)
            .Where(r => r.ProjectId == projectId)
            .OrderBy(r => r.Id)
            .ToListAsync();
    }

    private Task<int> Step(string code, string flowCode = ProcessFlowCodes.NewRequest) =>
        TestProjects.StepIdAsync(database, code, flowCode);

    private const string AddOn = ProcessFlowCodes.AddOn;
    private const string Improvement = ProcessFlowCodes.ProcessImprovement;

    // ---- Complete ----

    [Fact]
    public async Task Complete_ResponsibleRole_MovesToTheNextStepAndWritesHistory()
    {
        var projectId = await TestProjects.CreateAsync(database, "1");
        await using var analyst = await SignInAsync(TestProjects.Analyst);

        var result = await analyst.Service.CompleteStepAsync(
            projectId, new CompleteStepDto { ExpectedStepId = await Step("1"), Note = "  Form dolduruldu.  " }, default);

        Assert.Equal(ProjectStatus.Active, result.Status);
        Assert.Equal("2", result.CurrentStep!.Code);

        var record = Assert.Single(await RecordsAsync(projectId));
        Assert.Equal("1", record.ProcessStep.Code);
        Assert.Equal(StepAction.Completed, record.Action);
        Assert.Equal("Form dolduruldu.", record.Note);
        Assert.Equal(await TestProjects.UserIdAsync(database, TestProjects.Analyst), record.PerformedById);
    }

    [Fact]
    public async Task Complete_OtherRole_IsForbidden()
    {
        var projectId = await TestProjects.CreateAsync(database, "1");
        await using var developer = await SignInAsync(TestProjects.Developer);

        var error = await Assert.ThrowsAsync<ForbiddenException>(async () => await developer.Service.CompleteStepAsync(
            projectId, new CompleteStepDto { ExpectedStepId = await Step("1") }, default));

        Assert.Equal("Bu adım İş Analisti tarafından yapılır.", error.Message);
        Assert.Empty(await RecordsAsync(projectId));
    }

    [Fact]
    public async Task Complete_ApprovalStep_OnlyTheApproverCloses()
    {
        // Process improvement 8: the analyst prepares the standard, the department manager puts it into force.
        var projectId = await TestProjects.CreateAsync(database, "8", flowCode: Improvement);
        var step8 = await Step("8", Improvement);

        await using (var analyst = await SignInAsync(TestProjects.Analyst))
        {
            var error = await Assert.ThrowsAsync<ForbiddenException>(async () => await analyst.Service.CompleteStepAsync(
                projectId, new CompleteStepDto { ExpectedStepId = step8 }, default));
            Assert.Equal("Bu adım Yazılım Departman Yöneticisi tarafından onaylanır.", error.Message);
        }

        await using var manager = await SignInAsync(TestProjects.Manager);
        var result = await manager.Service.CompleteStepAsync(projectId, new CompleteStepDto { ExpectedStepId = step8 }, default);

        Assert.Equal(ProjectStatus.Closed, result.Status);
    }

    [Fact]
    public async Task Complete_DecisionStep_MustBeAnsweredInstead()
    {
        var projectId = await TestProjects.CreateAsync(database, "17");
        await using var developer = await SignInAsync(TestProjects.Developer);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(async () => await developer.Service.CompleteStepAsync(
            projectId, new CompleteStepDto { ExpectedStepId = await Step("17") }, default));

        Assert.Equal("Bu adım bir karar adımıdır; Evet/Hayır cevabı verilmelidir.", error.Message);
    }

    [Fact]
    public async Task Complete_ArrivingAtAnApprovalStep_NotifiesTheApproverRole()
    {
        // 9 → 10: the offer step is approved by the department manager.
        var projectId = await TestProjects.CreateAsync(database, "9");
        var code = (await TestProjects.GetAsync(database, projectId)).Code;
        await using var projectManager = await SignInAsync(TestProjects.ProjectManager);

        await projectManager.Service.CompleteStepAsync(projectId, new CompleteStepDto { ExpectedStepId = await Step("9") }, default);

        var sent = Assert.Single(projectManager.Notifications.Sent);
        Assert.Equal(UserRole.SoftwareDepartmentManager, sent.Role);
        Assert.Equal($"{code} · 10 onayınızı bekliyor.", sent.Message);
    }

    [Fact]
    public async Task Complete_LastStep_ClosesTheProjectAndNotifiesAnalystAndProjectManager()
    {
        var projectId = await TestProjects.CreateAsync(database, "19");
        var code = (await TestProjects.GetAsync(database, projectId)).Code;
        await using var projectManager = await SignInAsync(TestProjects.ProjectManager);

        var result = await projectManager.Service.CompleteStepAsync(
            projectId, new CompleteStepDto { ExpectedStepId = await Step("19") }, default);

        Assert.Equal(ProjectStatus.Closed, result.Status);
        Assert.Null(result.CurrentStep);
        Assert.Equal(100, result.ProgressPercent);

        var sent = Assert.Single(projectManager.Notifications.Sent);
        Assert.Equal(
            [await TestProjects.UserIdAsync(database, TestProjects.Analyst), await TestProjects.UserIdAsync(database, TestProjects.ProjectManager)],
            sent.UserIds.Order());
        Assert.Equal($"{code} kapandı: Kapandı.", sent.Message);
    }

    // ---- Conditional steps (docs/process-api.md → "Adım koşulları") ----

    [Theory]
    [InlineData(RequestSource.External, true, "4")]  // the customer suitability survey
    [InlineData(RequestSource.Internal, true, "5")]  // only for external customers
    [InlineData(RequestSource.External, false, "5")] // "doğrudan proje": no offer, no survey
    public async Task Complete_Step3_GoesToSuitabilityOnlyForExternalRequestsWithAnOffer(
        RequestSource source, bool requiresOffer, string next)
    {
        var projectId = await TestProjects.CreateAsync(database, "3", source: source, requiresOffer: requiresOffer);
        await using var analyst = await SignInAsync(TestProjects.Analyst);

        var result = await analyst.Service.CompleteStepAsync(
            projectId, new CompleteStepDto { ExpectedStepId = await Step("3") }, default);

        Assert.Equal(next, result.CurrentStep!.Code);
    }

    [Fact]
    public async Task Complete_Step9_WithoutAnOffer_SkipsTheOfferStageAndItsApproval()
    {
        var projectId = await TestProjects.CreateAsync(database, "9", requiresOffer: false);
        await using var projectManager = await SignInAsync(TestProjects.ProjectManager);

        var result = await projectManager.Service.CompleteStepAsync(
            projectId, new CompleteStepDto { ExpectedStepId = await Step("9") }, default);

        Assert.Equal("12", result.CurrentStep!.Code);
        Assert.Equal(
            [await Step("4"), await Step("10"), await Step("11")],
            result.SkippedStepIds);
        // Nobody is asked to approve an offer that is not prepared.
        Assert.Empty(projectManager.Notifications.Sent);
        // Only the record of 9 is written; skipped steps leave no history.
        Assert.Equal(["9"], (await RecordsAsync(projectId)).Select(r => r.ProcessStep.Code));
    }

    [Theory]
    [InlineData(1, "10")] // level 1: no scope document, no offer
    [InlineData(2, "8")]  // level 2: scope and effort
    [InlineData(3, "8")]
    public async Task Complete_AddOnStep7_GoesToScopeOnlyFromLevel2(int level, string next)
    {
        var projectId = await TestProjects.CreateAsync(database, "7", level: level, flowCode: AddOn);
        await using var projectManager = await SignInAsync(TestProjects.ProjectManager);

        var result = await projectManager.Service.CompleteStepAsync(
            projectId, new CompleteStepDto { ExpectedStepId = await Step("7", AddOn) }, default);

        Assert.Equal(next, result.CurrentStep!.Code);
    }

    [Theory]
    [InlineData(2, "10")] // level 2: the scope goes to the customer without an offer
    [InlineData(3, "9")]  // level 3: the sales department prepares an offer
    public async Task AnswerYes_AddOnStep8_GoesToTheOfferOnlyOnLevel3(int level, string next)
    {
        var projectId = await TestProjects.CreateAsync(database, "8", level: level, flowCode: AddOn);
        await using var projectManager = await SignInAsync(TestProjects.ProjectManager);

        var result = await projectManager.Service.AnswerStepAsync(projectId,
            new AnswerStepDto { ExpectedStepId = await Step("8", AddOn), Answer = StepAnswer.Yes }, default);

        Assert.Equal(next, result.CurrentStep!.Code);
    }

    // ---- Checks every command shares (in order: 404, 409 not active, 409 step changed, 403) ----

    [Fact]
    public async Task Command_ProjectNotFound_Is404()
    {
        await using var analyst = await SignInAsync(TestProjects.Analyst);

        await Assert.ThrowsAsync<NotFoundException>(async () => await analyst.Service.CompleteStepAsync(
            999_999, new CompleteStepDto { ExpectedStepId = 1 }, default));
    }

    [Fact]
    public async Task Command_InactiveProject_Is409()
    {
        var projectId = await TestProjects.CreateAsync(database, stepCode: null, ProjectStatus.Lost);
        await using var manager = await SignInAsync(TestProjects.Manager);

        var error = await Assert.ThrowsAsync<ConflictException>(async () => await manager.Service.RejectAsync(
            projectId, new RejectProjectDto { ExpectedStepId = 1, Decision = TestDecisions.Form() }, default));

        Assert.Equal("Proje aktif değil; süreçte işlem yapılamaz.", error.Message);
    }

    [Fact]
    public async Task Command_FromAnOldScreen_Is409()
    {
        // The user still sees 1, but the project has already moved to 2.
        var projectId = await TestProjects.CreateAsync(database, "2");
        await using var analyst = await SignInAsync(TestProjects.Analyst);

        var error = await Assert.ThrowsAsync<ConflictException>(async () => await analyst.Service.CompleteStepAsync(
            projectId, new CompleteStepDto { ExpectedStepId = await Step("1") }, default));

        Assert.Equal("Proje bu arada başka bir adıma geçti. Sayfayı yenileyin.", error.Message);
        Assert.Empty(await RecordsAsync(projectId));
    }

    [Fact]
    public async Task Command_NobodySignedIn_IsForbidden()
    {
        var projectId = await TestProjects.CreateAsync(database, "1");
        await using var nobody = await SignInAsync(email: null);

        var error = await Assert.ThrowsAsync<ForbiddenException>(async () => await nobody.Service.CompleteStepAsync(
            projectId, new CompleteStepDto { ExpectedStepId = await Step("1") }, default));

        Assert.Equal("İşlem yapmak için giriş yapılmalıdır.", error.Message);
    }

    [Fact]
    public async Task Command_TwoUsersAtTheSameMoment_SecondOneFailsAndWritesNothing()
    {
        var projectId = await TestProjects.CreateAsync(database, "1");
        var step1 = await Step("1");

        // User A opened the project (it is loaded and tracked) ...
        await using var userA = await SignInAsync(TestProjects.Analyst);
        await userA.Db.Projects.SingleAsync(p => p.Id == projectId);

        // ... user B completes the step in the meantime ...
        await using (var userB = await SignInAsync(TestProjects.Analyst))
        {
            await userB.Service.CompleteStepAsync(projectId, new CompleteStepDto { ExpectedStepId = step1 }, default);
        }

        // ... then A completes the same step from the same screen: RowVersion stops it (409 in the API).
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(async () => await userA.Service.CompleteStepAsync(
            projectId, new CompleteStepDto { ExpectedStepId = step1 }, default));

        Assert.Single(await RecordsAsync(projectId));
        Assert.Equal("2", (await TestProjects.GetAsync(database, projectId)).CurrentStep!.Code);
    }

    // ---- Answer: Yes ----

    [Fact]
    public async Task AnswerYes_MovesToTheNextStep()
    {
        var projectId = await TestProjects.CreateAsync(database, "17");
        await using var developer = await SignInAsync(TestProjects.Developer);

        var result = await developer.Service.AnswerStepAsync(
            projectId, new AnswerStepDto { ExpectedStepId = await Step("17"), Answer = StepAnswer.Yes }, default);

        Assert.Equal("18", result.CurrentStep!.Code);
        Assert.Equal(StepAction.Yes, Assert.Single(await RecordsAsync(projectId)).Action);
    }

    [Fact]
    public async Task AnswerYes_WithAReturnStep_IsRejected()
    {
        var projectId = await TestProjects.CreateAsync(database, "17");
        await using var developer = await SignInAsync(TestProjects.Developer);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(async () => await developer.Service.AnswerStepAsync(projectId,
            new AnswerStepDto { ExpectedStepId = await Step("17"), Answer = StepAnswer.Yes, JumpTargetStepId = await Step("16") },
            default));

        Assert.Equal("Evet cevabında dönüş adımı seçilmez.", error.Message);
    }

    [Fact]
    public async Task Answer_NonDecisionStep_IsRejected()
    {
        var projectId = await TestProjects.CreateAsync(database, "1");
        await using var analyst = await SignInAsync(TestProjects.Analyst);

        await Assert.ThrowsAsync<BusinessRuleException>(async () => await analyst.Service.AnswerStepAsync(
            projectId, new AnswerStepDto { ExpectedStepId = await Step("1"), Answer = StepAnswer.Yes }, default));
    }

    [Fact]
    public async Task Answer_OfferApproval_SalesCannotButManagerCan()
    {
        // 10: sales prepares the offer, the department manager approves it.
        var projectId = await TestProjects.CreateAsync(database, "10", source: RequestSource.External);
        var step10 = await Step("10");

        await using (var sales = await SignInAsync(TestProjects.Sales))
        {
            var error = await Assert.ThrowsAsync<ForbiddenException>(async () => await sales.Service.AnswerStepAsync(
                projectId, new AnswerStepDto { ExpectedStepId = step10, Answer = StepAnswer.Yes }, default));
            Assert.Equal("Bu adım Yazılım Departman Yöneticisi tarafından onaylanır.", error.Message);
        }

        await using var manager = await SignInAsync(TestProjects.Manager);
        var result = await manager.Service.AnswerStepAsync(
            projectId, new AnswerStepDto { ExpectedStepId = step10, Answer = StepAnswer.Yes }, default);

        Assert.Equal("11", result.CurrentStep!.Code);
    }

    // ---- Answer: No (three outcomes) ----

    [Theory]
    [InlineData(ProcessFlowCodes.NewRequest, "4", TestProjects.Analyst, ProjectStatus.Rejected, "Reddedildi")]
    [InlineData(ProcessFlowCodes.NewRequest, "11", TestProjects.Sales, ProjectStatus.Lost, "Kaybedildi")]
    [InlineData(ProcessFlowCodes.ProcessImprovement, "2", TestProjects.Manager, ProjectStatus.Rejected, "Reddedildi")]
    public async Task AnswerNo_ClosingStep_ClosesTheProjectWithItsOutcome(
        string flowCode, string stepCode, string answeredBy, ProjectStatus outcome, string outcomeName)
    {
        var projectId = await TestProjects.CreateAsync(database, stepCode, source: RequestSource.External, flowCode: flowCode);
        var code = (await TestProjects.GetAsync(database, projectId)).Code;
        await using var user = await SignInAsync(answeredBy);

        var result = await user.Service.AnswerStepAsync(projectId,
            new AnswerStepDto
            {
                ExpectedStepId = await Step(stepCode, flowCode),
                Answer = StepAnswer.No,
                // Required to close a request (D2); the process improvement flow ignores it.
                ClosingDecision = TestDecisions.Form(),
            }, default);

        Assert.Equal(outcome, result.Status);
        Assert.Null(result.CurrentStep);

        var record = Assert.Single(await RecordsAsync(projectId));
        Assert.Equal(StepAction.No, record.Action);
        Assert.Equal(stepCode, record.ProcessStep.Code);

        Assert.Equal($"{code} kapandı: {outcomeName}.", Assert.Single(user.Notifications.Sent).Message);
    }

    [Fact]
    public async Task AnswerNo_Step10_ScopeChanged_GoesBackTo9AndNotifiesTheAnalyst()
    {
        var projectId = await TestProjects.CreateAsync(database, "10", source: RequestSource.External);
        var code = (await TestProjects.GetAsync(database, projectId)).Code;
        await using var manager = await SignInAsync(TestProjects.Manager);

        var result = await manager.Service.AnswerStepAsync(projectId,
            new AnswerStepDto { ExpectedStepId = await Step("10"), Answer = StepAnswer.No, JumpTargetStepId = await Step("9") },
            default);

        Assert.Equal(ProjectStatus.Active, result.Status);
        Assert.Equal("9", result.CurrentStep!.Code);
        Assert.Equal("9", Assert.Single(await RecordsAsync(projectId)).JumpedToStep!.Code);

        var sent = Assert.Single(manager.Notifications.Sent);
        Assert.Equal([await TestProjects.UserIdAsync(database, TestProjects.Analyst)], sent.UserIds);
        Assert.Equal($"{code} · 10 adımından 9 adımına dönüldü.", sent.Message);
    }

    [Theory]
    [InlineData("5")] // target partly met: repeat the pilot
    [InlineData("3")] // target not met: back to the study
    public async Task AnswerNo_ImprovementStep7_GoesBackToTheChosenOption(string target)
    {
        var projectId = await TestProjects.CreateAsync(database, "7", flowCode: Improvement);
        await using var analyst = await SignInAsync(TestProjects.Analyst);

        var result = await analyst.Service.AnswerStepAsync(projectId,
            new AnswerStepDto
            {
                ExpectedStepId = await Step("7", Improvement),
                Answer = StepAnswer.No,
                JumpTargetStepId = await Step(target, Improvement),
            },
            default);

        Assert.Equal(target, result.CurrentStep!.Code);
    }

    [Theory]
    [InlineData(null)] // no option chosen
    [InlineData("6")]  // an earlier step that is not one of the options
    [InlineData("8")]  // a later step
    public async Task AnswerNo_ImprovementStep7_WithoutAValidOption_IsRejected(string? target)
    {
        var projectId = await TestProjects.CreateAsync(database, "7", flowCode: Improvement);
        await using var analyst = await SignInAsync(TestProjects.Analyst);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(async () => await analyst.Service.AnswerStepAsync(projectId,
            new AnswerStepDto
            {
                ExpectedStepId = await Step("7", Improvement),
                Answer = StepAnswer.No,
                JumpTargetStepId = target is null ? null : await Step(target, Improvement),
            },
            default));

        Assert.Equal("Geçerli bir dönüş seçeneği seçilmelidir.", error.Message);
        Assert.Empty(await RecordsAsync(projectId));
    }

    [Fact]
    public async Task AnswerNo_StepWithoutOutcomeOrOptions_StaysOnTheStep()
    {
        // Add-on 8: the scope document is revised until the project manager approves it.
        var projectId = await TestProjects.CreateAsync(database, "8", level: 2, flowCode: AddOn);
        var before = await TestProjects.GetAsync(database, projectId);
        await using var projectManager = await SignInAsync(TestProjects.ProjectManager);

        var result = await projectManager.Service.AnswerStepAsync(projectId,
            new AnswerStepDto { ExpectedStepId = await Step("8", AddOn), Answer = StepAnswer.No }, default);

        Assert.Equal("8", result.CurrentStep!.Code);
        Assert.Equal(StepAction.No, Assert.Single(await RecordsAsync(projectId)).Action);
        Assert.Empty(projectManager.Notifications.Sent);

        // The project row is still updated, so RowVersion protects this answer from a parallel one.
        var after = await TestProjects.GetAsync(database, projectId);
        Assert.NotEqual(before.RowVersion, after.RowVersion);
    }

    [Fact]
    public async Task AnswerNo_StepWithoutOptions_RejectsAReturnStep()
    {
        var projectId = await TestProjects.CreateAsync(database, "8", level: 2, flowCode: AddOn);
        await using var projectManager = await SignInAsync(TestProjects.ProjectManager);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(async () => await projectManager.Service.AnswerStepAsync(projectId,
            new AnswerStepDto
            {
                ExpectedStepId = await Step("8", AddOn),
                Answer = StepAnswer.No,
                JumpTargetStepId = await Step("7", AddOn),
            },
            default));

        Assert.Equal("Bu adımda dönüş seçeneği yok; dönüş adımı seçilmez.", error.Message);
    }

    // ---- Reject ----

    [Fact]
    public async Task Reject_Manager_ClosesAsRejectedWithTheReasonOnTheCurrentStep()
    {
        var projectId = await TestProjects.CreateAsync(database, "14");
        var code = (await TestProjects.GetAsync(database, projectId)).Code;
        await using var manager = await SignInAsync(TestProjects.Manager);

        var result = await manager.Service.RejectAsync(projectId,
            new RejectProjectDto { ExpectedStepId = await Step("14"), Decision = TestDecisions.Form("  Bir aydır cevap alınamadı.  ") }, default);

        Assert.Equal(ProjectStatus.Rejected, result.Status);
        Assert.Null(result.CurrentStep);

        var record = Assert.Single(await RecordsAsync(projectId));
        Assert.Equal(StepAction.Rejected, record.Action);
        Assert.Equal("14", record.ProcessStep.Code);
        Assert.Equal("Bir aydır cevap alınamadı.", record.Note);

        Assert.Equal($"{code} kapandı: Reddedildi.", Assert.Single(manager.Notifications.Sent).Message);
    }

    [Theory]
    [InlineData(TestProjects.Analyst)]
    [InlineData(TestProjects.ProjectManager)]
    public async Task Reject_OtherRole_IsForbidden(string email)
    {
        var projectId = await TestProjects.CreateAsync(database, "14");
        await using var user = await SignInAsync(email);

        var error = await Assert.ThrowsAsync<ForbiddenException>(async () => await user.Service.RejectAsync(projectId,
            new RejectProjectDto { ExpectedStepId = await Step("14"), Decision = TestDecisions.Form() }, default));

        Assert.Equal("Talebi yalnızca Yazılım Departman Yöneticisi reddedebilir.", error.Message);
        Assert.Equal(ProjectStatus.Active, (await TestProjects.GetAsync(database, projectId)).Status);
    }
}
