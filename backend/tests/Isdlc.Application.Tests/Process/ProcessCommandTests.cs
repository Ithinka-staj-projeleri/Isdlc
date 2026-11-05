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
/// Complete, answer and reject on a real database. Every test creates its own project (TestProjects),
/// so seed projects are never changed. Expectations: docs/process-api.md and docs/data-model.md.
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

    private Task<int> Step(string code) => TestProjects.StepIdAsync(database, code);

    // ---- Complete ----

    [Fact]
    public async Task Complete_ResponsibleRole_MovesToTheNextStepAndWritesHistory()
    {
        var projectId = await TestProjects.CreateAsync(database, "1.1");
        await using var analyst = await SignInAsync(TestProjects.Analyst);

        var result = await analyst.Service.CompleteStepAsync(
            projectId, new CompleteStepDto { ExpectedStepId = await Step("1.1"), Note = "  Form dolduruldu.  " }, default);

        Assert.Equal(ProjectStatus.Active, result.Status);
        Assert.Equal("1.2", result.CurrentStep!.Code);

        var record = Assert.Single(await RecordsAsync(projectId));
        Assert.Equal("1.1", record.ProcessStep.Code);
        Assert.Equal(StepAction.Completed, record.Action);
        Assert.Equal("Form dolduruldu.", record.Note);
        Assert.Equal(await TestProjects.UserIdAsync(database, TestProjects.Analyst), record.PerformedById);
    }

    [Fact]
    public async Task Complete_OtherRole_IsForbidden()
    {
        var projectId = await TestProjects.CreateAsync(database, "1.1");
        await using var developer = await SignInAsync(TestProjects.Developer);

        var error = await Assert.ThrowsAsync<ForbiddenException>(async () => await developer.Service.CompleteStepAsync(
            projectId, new CompleteStepDto { ExpectedStepId = await Step("1.1") }, default));

        Assert.Equal("Bu adım İş Analisti tarafından yapılır.", error.Message);
        Assert.Empty(await RecordsAsync(projectId));
    }

    [Fact]
    public async Task Complete_Step51_SalesPreparesButOnlyTheManagerCloses()
    {
        var projectId = await TestProjects.CreateAsync(database, "5.1");
        var step51 = await Step("5.1");

        await using (var sales = await SignInAsync(TestProjects.Sales))
        {
            var error = await Assert.ThrowsAsync<ForbiddenException>(async () => await sales.Service.CompleteStepAsync(
                projectId, new CompleteStepDto { ExpectedStepId = step51 }, default));
            Assert.Equal("Bu adım Yazılım Departman Yöneticisi tarafından onaylanır.", error.Message);
        }

        await using var manager = await SignInAsync(TestProjects.Manager);
        var result = await manager.Service.CompleteStepAsync(projectId, new CompleteStepDto { ExpectedStepId = step51 }, default);

        Assert.Equal("5.2", result.CurrentStep!.Code);
    }

    [Fact]
    public async Task Complete_DecisionStep_MustBeAnsweredInstead()
    {
        var projectId = await TestProjects.CreateAsync(database, "2.2");
        await using var analyst = await SignInAsync(TestProjects.Analyst);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(async () => await analyst.Service.CompleteStepAsync(
            projectId, new CompleteStepDto { ExpectedStepId = await Step("2.2") }, default));

        Assert.Equal("Bu adım bir karar adımıdır; Evet/Hayır cevabı verilmelidir.", error.Message);
    }

    [Fact]
    public async Task Complete_ArrivingAtAnApprovalStep_NotifiesTheApproverRole()
    {
        var projectId = await TestProjects.CreateAsync(database, "4.4");
        var code = (await TestProjects.GetAsync(database, projectId)).Code;
        await using var analyst = await SignInAsync(TestProjects.Analyst);

        await analyst.Service.CompleteStepAsync(projectId, new CompleteStepDto { ExpectedStepId = await Step("4.4") }, default);

        var sent = Assert.Single(analyst.Notifications.Sent);
        Assert.Equal(UserRole.SoftwareDepartmentManager, sent.Role);
        Assert.Equal($"{code} · 4.5 onayınızı bekliyor.", sent.Message);
    }

    [Fact]
    public async Task Complete_LastStep_ClosesTheProjectAndNotifiesAnalystAndProjectManager()
    {
        var projectId = await TestProjects.CreateAsync(database, "13.5");
        var code = (await TestProjects.GetAsync(database, projectId)).Code;
        await using var analyst = await SignInAsync(TestProjects.Analyst);

        var result = await analyst.Service.CompleteStepAsync(
            projectId, new CompleteStepDto { ExpectedStepId = await Step("13.5") }, default);

        Assert.Equal(ProjectStatus.Closed, result.Status);
        Assert.Null(result.CurrentStep);
        Assert.Equal(100, result.ProgressPercent);

        var sent = Assert.Single(analyst.Notifications.Sent);
        Assert.Equal(
            [await TestProjects.UserIdAsync(database, TestProjects.Analyst), await TestProjects.UserIdAsync(database, TestProjects.ProjectManager)],
            sent.UserIds.Order());
        Assert.Equal($"{code} kapandı: Kapandı.", sent.Message);
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
            projectId, new RejectProjectDto { ExpectedStepId = 1, Reason = "Gerekçe" }, default));

        Assert.Equal("Proje aktif değil; süreçte işlem yapılamaz.", error.Message);
    }

    [Fact]
    public async Task Command_FromAnOldScreen_Is409()
    {
        // The user still sees 1.1, but the project has already moved to 1.2.
        var projectId = await TestProjects.CreateAsync(database, "1.2");
        await using var analyst = await SignInAsync(TestProjects.Analyst);

        var error = await Assert.ThrowsAsync<ConflictException>(async () => await analyst.Service.CompleteStepAsync(
            projectId, new CompleteStepDto { ExpectedStepId = await Step("1.1") }, default));

        Assert.Equal("Proje bu arada başka bir adıma geçti. Sayfayı yenileyin.", error.Message);
        Assert.Empty(await RecordsAsync(projectId));
    }

    [Fact]
    public async Task Command_NobodySignedIn_IsForbidden()
    {
        var projectId = await TestProjects.CreateAsync(database, "1.1");
        await using var nobody = await SignInAsync(email: null);

        var error = await Assert.ThrowsAsync<ForbiddenException>(async () => await nobody.Service.CompleteStepAsync(
            projectId, new CompleteStepDto { ExpectedStepId = await Step("1.1") }, default));

        Assert.Equal("İşlem yapmak için giriş yapılmalıdır.", error.Message);
    }

    [Fact]
    public async Task Command_TwoUsersAtTheSameMoment_SecondOneFailsAndWritesNothing()
    {
        var projectId = await TestProjects.CreateAsync(database, "1.1");
        var step11 = await Step("1.1");

        // User A opened the project (it is loaded and tracked) ...
        await using var userA = await SignInAsync(TestProjects.Analyst);
        await userA.Db.Projects.SingleAsync(p => p.Id == projectId);

        // ... user B completes the step in the meantime ...
        await using (var userB = await SignInAsync(TestProjects.Analyst))
        {
            await userB.Service.CompleteStepAsync(projectId, new CompleteStepDto { ExpectedStepId = step11 }, default);
        }

        // ... then A completes the same step from the same screen: RowVersion stops it (409 in the API).
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(async () => await userA.Service.CompleteStepAsync(
            projectId, new CompleteStepDto { ExpectedStepId = step11 }, default));

        Assert.Single(await RecordsAsync(projectId));
        Assert.Equal("1.2", (await TestProjects.GetAsync(database, projectId)).CurrentStep!.Code);
    }

    // ---- Answer: Yes ----

    [Fact]
    public async Task AnswerYes_MovesToTheNextStep()
    {
        var projectId = await TestProjects.CreateAsync(database, "1.2");
        await using var analyst = await SignInAsync(TestProjects.Analyst);

        var result = await analyst.Service.AnswerStepAsync(
            projectId, new AnswerStepDto { ExpectedStepId = await Step("1.2"), Answer = StepAnswer.Yes }, default);

        Assert.Equal("1.3", result.CurrentStep!.Code);
        Assert.Equal(StepAction.Yes, Assert.Single(await RecordsAsync(projectId)).Action);
    }

    [Fact]
    public async Task AnswerYes_WithAReturnStep_IsRejected()
    {
        var projectId = await TestProjects.CreateAsync(database, "4.7");
        await using var analyst = await SignInAsync(TestProjects.Analyst);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(async () => await analyst.Service.AnswerStepAsync(projectId,
            new AnswerStepDto { ExpectedStepId = await Step("4.7"), Answer = StepAnswer.Yes, JumpTargetStepId = await Step("4.1") },
            default));

        Assert.Equal("Evet cevabında dönüş adımı seçilmez.", error.Message);
    }

    [Fact]
    public async Task Answer_NonDecisionStep_IsRejected()
    {
        var projectId = await TestProjects.CreateAsync(database, "1.1");
        await using var analyst = await SignInAsync(TestProjects.Analyst);

        await Assert.ThrowsAsync<BusinessRuleException>(async () => await analyst.Service.AnswerStepAsync(
            projectId, new AnswerStepDto { ExpectedStepId = await Step("1.1"), Answer = StepAnswer.Yes }, default));
    }

    // ---- Answer: No (three outcomes) ----

    [Theory]
    [InlineData("1.2", TestProjects.Analyst, ProjectStatus.Redirected, "Yönlendirildi")]
    [InlineData("3.2", TestProjects.Manager, ProjectStatus.Rejected, "Reddedildi")] // approval step: the manager answers
    [InlineData("5.3", TestProjects.Analyst, ProjectStatus.Lost, "Kaybedildi")]
    public async Task AnswerNo_ClosingStep_ClosesTheProjectWithItsOutcome(
        string stepCode, string answeredBy, ProjectStatus outcome, string outcomeName)
    {
        var projectId = await TestProjects.CreateAsync(database, stepCode);
        var code = (await TestProjects.GetAsync(database, projectId)).Code;
        await using var user = await SignInAsync(answeredBy);

        var result = await user.Service.AnswerStepAsync(
            projectId, new AnswerStepDto { ExpectedStepId = await Step(stepCode), Answer = StepAnswer.No }, default);

        Assert.Equal(outcome, result.Status);
        Assert.Null(result.CurrentStep);

        var record = Assert.Single(await RecordsAsync(projectId));
        Assert.Equal(StepAction.No, record.Action);
        Assert.Equal(stepCode, record.ProcessStep.Code);

        Assert.Equal($"{code} kapandı: {outcomeName}.", Assert.Single(user.Notifications.Sent).Message);
    }

    [Fact]
    public async Task AnswerNo_Step47_GoesBackTo41AndNotifiesTheAnalyst()
    {
        var projectId = await TestProjects.CreateAsync(database, "4.7");
        var code = (await TestProjects.GetAsync(database, projectId)).Code;
        await using var analyst = await SignInAsync(TestProjects.Analyst);

        var result = await analyst.Service.AnswerStepAsync(projectId,
            new AnswerStepDto { ExpectedStepId = await Step("4.7"), Answer = StepAnswer.No, JumpTargetStepId = await Step("4.1") },
            default);

        Assert.Equal(ProjectStatus.Active, result.Status);
        Assert.Equal("4.1", result.CurrentStep!.Code);
        Assert.Equal("4.1", Assert.Single(await RecordsAsync(projectId)).JumpedToStep!.Code);

        var sent = Assert.Single(analyst.Notifications.Sent);
        Assert.Equal([await TestProjects.UserIdAsync(database, TestProjects.Analyst)], sent.UserIds);
        Assert.Equal($"{code} · 4.7 adımından 4.1 adımına dönüldü.", sent.Message);
    }

    [Theory]
    [InlineData("4.1")]
    [InlineData("4.9")]
    public async Task AnswerNo_Step54_GoesBackToTheChosenOption(string target)
    {
        var projectId = await TestProjects.CreateAsync(database, "5.4");
        await using var analyst = await SignInAsync(TestProjects.Analyst);

        var result = await analyst.Service.AnswerStepAsync(projectId,
            new AnswerStepDto { ExpectedStepId = await Step("5.4"), Answer = StepAnswer.No, JumpTargetStepId = await Step(target) },
            default);

        Assert.Equal(target, result.CurrentStep!.Code);
    }

    [Theory]
    [InlineData(null)]  // no option chosen
    [InlineData("4.5")] // a step that is not one of the options
    public async Task AnswerNo_Step54_WithoutAValidOption_IsRejected(string? target)
    {
        var projectId = await TestProjects.CreateAsync(database, "5.4");
        await using var analyst = await SignInAsync(TestProjects.Analyst);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(async () => await analyst.Service.AnswerStepAsync(projectId,
            new AnswerStepDto
            {
                ExpectedStepId = await Step("5.4"),
                Answer = StepAnswer.No,
                JumpTargetStepId = target is null ? null : await Step(target),
            },
            default));

        Assert.Equal("Geçerli bir dönüş seçeneği seçilmelidir.", error.Message);
        Assert.Empty(await RecordsAsync(projectId));
    }

    [Fact]
    public async Task AnswerNo_StepWithoutOutcomeOrOptions_StaysOnTheStep()
    {
        // 2.2: the project waits on the same step until it is answered again.
        var projectId = await TestProjects.CreateAsync(database, "2.2");
        var before = await TestProjects.GetAsync(database, projectId);
        await using var analyst = await SignInAsync(TestProjects.Analyst);

        var result = await analyst.Service.AnswerStepAsync(
            projectId, new AnswerStepDto { ExpectedStepId = await Step("2.2"), Answer = StepAnswer.No }, default);

        Assert.Equal("2.2", result.CurrentStep!.Code);
        Assert.Equal(StepAction.No, Assert.Single(await RecordsAsync(projectId)).Action);
        Assert.Empty(analyst.Notifications.Sent);

        // The project row is still updated, so RowVersion protects this answer from a parallel one.
        var after = await TestProjects.GetAsync(database, projectId);
        Assert.NotEqual(before.RowVersion, after.RowVersion);
    }

    [Fact]
    public async Task AnswerNo_StepWithoutOptions_RejectsAReturnStep()
    {
        var projectId = await TestProjects.CreateAsync(database, "2.2");
        await using var analyst = await SignInAsync(TestProjects.Analyst);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(async () => await analyst.Service.AnswerStepAsync(projectId,
            new AnswerStepDto { ExpectedStepId = await Step("2.2"), Answer = StepAnswer.No, JumpTargetStepId = await Step("1.1") },
            default));

        Assert.Equal("Bu adımda dönüş seçeneği yok; dönüş adımı seçilmez.", error.Message);
    }

    [Fact]
    public async Task Answer_ApprovalStep45_AnalystCannotButManagerCan()
    {
        var projectId = await TestProjects.CreateAsync(database, "4.5");
        var step45 = await Step("4.5");

        await using (var analyst = await SignInAsync(TestProjects.Analyst))
        {
            await Assert.ThrowsAsync<ForbiddenException>(async () => await analyst.Service.AnswerStepAsync(
                projectId, new AnswerStepDto { ExpectedStepId = step45, Answer = StepAnswer.Yes }, default));
        }

        await using var manager = await SignInAsync(TestProjects.Manager);
        var result = await manager.Service.AnswerStepAsync(
            projectId, new AnswerStepDto { ExpectedStepId = step45, Answer = StepAnswer.Yes }, default);

        Assert.Equal("4.6", result.CurrentStep!.Code);
    }

    // ---- Reject ----

    [Fact]
    public async Task Reject_Manager_ClosesAsRejectedWithTheReasonOnTheCurrentStep()
    {
        var projectId = await TestProjects.CreateAsync(database, "7.4");
        var code = (await TestProjects.GetAsync(database, projectId)).Code;
        await using var manager = await SignInAsync(TestProjects.Manager);

        var result = await manager.Service.RejectAsync(projectId,
            new RejectProjectDto { ExpectedStepId = await Step("7.4"), Reason = "  Bir aydır cevap alınamadı.  " }, default);

        Assert.Equal(ProjectStatus.Rejected, result.Status);
        Assert.Null(result.CurrentStep);

        var record = Assert.Single(await RecordsAsync(projectId));
        Assert.Equal(StepAction.Rejected, record.Action);
        Assert.Equal("7.4", record.ProcessStep.Code);
        Assert.Equal("Bir aydır cevap alınamadı.", record.Note);

        Assert.Equal($"{code} kapandı: Reddedildi.", Assert.Single(manager.Notifications.Sent).Message);
    }

    [Theory]
    [InlineData(TestProjects.Analyst)]
    [InlineData(TestProjects.ProjectManager)]
    public async Task Reject_OtherRole_IsForbidden(string email)
    {
        var projectId = await TestProjects.CreateAsync(database, "7.4");
        await using var user = await SignInAsync(email);

        var error = await Assert.ThrowsAsync<ForbiddenException>(async () => await user.Service.RejectAsync(projectId,
            new RejectProjectDto { ExpectedStepId = await Step("7.4"), Reason = "Gerekçe" }, default));

        Assert.Equal("Talebi yalnızca Yazılım Departman Yöneticisi reddedebilir.", error.Message);
        Assert.Equal(ProjectStatus.Active, (await TestProjects.GetAsync(database, projectId)).Status);
    }
}
