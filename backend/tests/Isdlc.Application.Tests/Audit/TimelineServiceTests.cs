using Isdlc.Application.Audit;
using Isdlc.Application.Audit.Dtos;
using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Common.Models;
using Isdlc.Application.Meetings;
using Isdlc.Application.Meetings.Dtos;
using Isdlc.Application.OpenIssues;
using Isdlc.Application.OpenIssues.Dtos;
using Isdlc.Application.Requirements;
using Isdlc.Application.Requirements.Dtos;
using Isdlc.Application.Risks;
using Isdlc.Application.Risks.Dtos;
using Isdlc.Application.Tests.Common;
using Isdlc.Domain.Meetings;
using Isdlc.Domain.OpenIssues;
using Isdlc.Domain.Process;
using Isdlc.Domain.Requirements;
using Isdlc.Domain.Risks;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Audit;

/// <summary>
/// A project's timeline on a real database. Records are made through the real services (so the audit log rows the
/// timeline reads are real), except step records, which are added directly. Expectations: docs/audit-api.md → Zaman çizelgesi.
/// The tests under "Cross tests" are the cross test (PLANNING.md): Dide → Erva's modules.
/// </summary>
[Collection("Database")]
public class TimelineServiceTests(DatabaseFixture database)
{
    private static readonly PageRequest FirstPage = new() { Page = 1, PageSize = 100 };

    private async Task<PagedResult<TimelineEventDto>> ListAsync(int projectId, PageRequest? pageRequest = null)
    {
        await using var scope = database.CreateScope();
        var service = new TimelineService(scope.ServiceProvider.GetRequiredService<IApplicationDbContext>());
        return await service.ListProjectTimelineAsync(projectId, pageRequest ?? FirstPage, CancellationToken.None);
    }

    private async Task<T> InScopeAsync<T>(Func<IApplicationDbContext, int, Task<T>> action)
    {
        var analystId = await TestProjects.UserIdAsync(database, TestProjects.Analyst);
        await using var scope = database.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(), analystId);
    }

    private Task<int> AddStepRecordAsync(int projectId, string stepCode, StepAction action, DateTime performedAt, string? note = null) =>
        InScopeAsync(async (db, userId) =>
        {
            var record = new StepRecord
            {
                ProjectId = projectId,
                ProcessStepId = await TestProjects.StepIdAsync(database, stepCode),
                Action = action,
                Note = note,
                PerformedById = userId,
                PerformedAt = performedAt,
            };
            db.StepRecords.Add(record);
            await db.SaveChangesAsync(CancellationToken.None);
            return record.Id;
        });

    [Fact]
    public async Task List_ProjectMissing_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => ListAsync(int.MaxValue));
    }

    [Fact]
    public async Task List_NewProject_HasOnlyTheCreatedEvent()
    {
        var projectId = await TestProjects.CreateAsync(database, "1");

        var result = await ListAsync(projectId);

        var created = Assert.Single(result.Items);
        Assert.Equal(TimelineEventKind.Created, created.Kind);
        Assert.Equal("Test projesi (1)", created.Title);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task List_StepRecords_NewestFirstWithStepTitleAndNote()
    {
        var projectId = await TestProjects.CreateAsync(database, "3");
        var now = DateTime.UtcNow;
        await AddStepRecordAsync(projectId, "1", StepAction.Completed, now.AddHours(-2));
        await AddStepRecordAsync(projectId, "2", StepAction.Completed, now.AddHours(-1), "Klasör açıldı.");

        var steps = (await ListAsync(projectId)).Items.Where(e => e.Kind == TimelineEventKind.Step).ToList();

        Assert.Equal(["2", "1"], steps.Select(s => s.RecordCode));
        Assert.Equal("Teklif / proje klasörü açılır", steps[0].Title);
        Assert.Equal("Klasör açıldı.", steps[0].Note);
        Assert.Equal(StepAction.Completed, steps[0].StepAction);
        Assert.Equal("Elif Yılmaz", steps[0].PerformedBy.FullName);
    }

    [Fact]
    public async Task List_MeetingWithADecision_ShowsTheMeetingOnItsDateAndTheDecision()
    {
        var projectId = await TestProjects.CreateAsync(database, "6");
        await InScopeAsync((db, userId) =>
            new MeetingService(db, new TestUser(userId), new RecordingNotificationService(), TimeProvider.System)
                .CreateAsync(projectId, new CreateMeetingDto
                {
                    Type = MeetingType.AnalysisSession,
                    Date = new DateOnly(2026, 11, 5),
                    RecordedByUserId = userId,
                    Participants = [new MeetingParticipantDto { Name = "Elif Yılmaz" }],
                    Agenda = "- Ekranlar\n- Raporlar",
                    Summary = "Ekranlar konuşuldu.",
                    Decisions = ["Raporlar PDF olarak da alınacak"],
                }, CancellationToken.None));

        var items = (await ListAsync(projectId)).Items;

        var meeting = Assert.Single(items, e => e.Kind == TimelineEventKind.Meeting);
        Assert.True(meeting.DateOnly);
        Assert.Equal(new DateTime(2026, 11, 5, 0, 0, 0, DateTimeKind.Utc), meeting.OccurredAt);
        Assert.Equal("TT-001", meeting.RecordCode);
        Assert.Equal("Ekranlar", meeting.Title);
        Assert.Equal(MeetingType.AnalysisSession, meeting.MeetingType);
        Assert.Equal("Ekranlar konuşuldu.", meeting.Note);

        var decision = Assert.Single(items, e => e.Kind == TimelineEventKind.Decision);
        Assert.Equal("Raporlar PDF olarak da alınacak", decision.Title);
        Assert.StartsWith("AK-", decision.RecordCode);
    }

    [Fact]
    public async Task List_RequirementApproved_IsAnEventButOtherStatusChangesAreNot()
    {
        var projectId = await TestProjects.CreateAsync(database, "7");
        var requirement = await InScopeAsync((db, userId) =>
            new RequirementService(db, new TestUser(userId)).CreateAsync(projectId, new CreateRequirementDto
            {
                Type = RequirementType.Functional, Title = "Mobil onay", Priority = RequirementPriority.MustHave,
            }, CancellationToken.None));

        async Task SetStatusAsync(RequirementStatus status) => await InScopeAsync((db, userId) =>
            new RequirementService(db, new TestUser(userId)).UpdateStatusAsync(projectId, requirement.Id,
                new UpdateRequirementStatusDto { Status = status }, CancellationToken.None));

        await SetStatusAsync(RequirementStatus.InReview);
        await SetStatusAsync(RequirementStatus.Approved);

        var items = (await ListAsync(projectId)).Items;

        var approved = Assert.Single(items, e => e.Kind == TimelineEventKind.RequirementApproved);
        Assert.Equal(requirement.Id, approved.RecordId);
        Assert.Equal(requirement.Code, approved.RecordCode);
        Assert.Equal("Mobil onay", approved.Title);
    }

    [Fact]
    public async Task List_Paging_CountsEveryEventAndReturnsThePage()
    {
        var projectId = await TestProjects.CreateAsync(database, "4");
        var now = DateTime.UtcNow;
        for (var i = 0; i < 3; i++)
        {
            await AddStepRecordAsync(projectId, "1", StepAction.Completed, now.AddMinutes(1 + i));
        }

        var page = await ListAsync(projectId, new PageRequest { Page = 2, PageSize = 2 });

        Assert.Equal(4, page.TotalCount); // three steps and the project's creation
        Assert.Equal(2, page.Items.Count);
        Assert.Equal(TimelineEventKind.Created, page.Items[^1].Kind);
    }

    // ---- Cross tests (PLANNING.md): Dide → Erva's modules ----

    private static UpdateRiskDto RiskForm(int ownerId, RiskStatus status, string title = "Veri taşıma gecikir") => new()
    {
        Title = title,
        Category = RiskCategory.Technical,
        Probability = 3,
        Impact = 4,
        OwnerUserId = ownerId,
        Status = status,
    };

    private Task<T> WithRisksAsync<T>(Func<IRiskService, int, Task<T>> action) =>
        InScopeAsync((db, userId) =>
            action(new RiskService(db, new TestUser(userId), new RecordingNotificationService()), userId));

    private Task<RiskDto> CreateRiskAsync(int projectId) =>
        WithRisksAsync((service, userId) =>
            service.CreateAsync(projectId, RiskForm(userId, RiskStatus.Open), CancellationToken.None));

    private Task<RiskDto> UpdateRiskAsync(int projectId, int riskId, RiskStatus status, string title = "Veri taşıma gecikir") =>
        WithRisksAsync((service, userId) =>
            service.UpdateAsync(projectId, riskId, RiskForm(userId, status, title), CancellationToken.None));

    private Task<RequirementDto> SetRequirementStatusAsync(int projectId, int requirementId, RequirementStatus status) =>
        InScopeAsync((db, userId) =>
            new RequirementService(db, new TestUser(userId)).UpdateStatusAsync(projectId, requirementId,
                new UpdateRequirementStatusDto { Status = status }, CancellationToken.None));

    private Task<OpenIssueDto> CreateOpenIssueAsync(int projectId, OpenIssueType type, string title) =>
        InScopeAsync((db, userId) =>
            new OpenIssueService(db, new TestUser(userId), new RecordingNotificationService(), TimeProvider.System)
                .CreateAsync(projectId, new CreateOpenIssueDto { Type = type, Title = title, OwnerUserId = userId },
                    CancellationToken.None));

    [Fact]
    public async Task List_RiskClosed_IsAnEventButMonitoringIsNot()
    {
        var projectId = await TestProjects.CreateAsync(database, "7");
        var risk = await CreateRiskAsync(projectId);

        await UpdateRiskAsync(projectId, risk.Id, RiskStatus.Monitoring);
        Assert.DoesNotContain((await ListAsync(projectId)).Items, e => e.Kind == TimelineEventKind.RiskClosed);

        await UpdateRiskAsync(projectId, risk.Id, RiskStatus.Closed);

        var closed = Assert.Single((await ListAsync(projectId)).Items, e => e.Kind == TimelineEventKind.RiskClosed);
        Assert.Equal(risk.Id, closed.RecordId);
        Assert.Equal(risk.Code, closed.RecordCode);
        Assert.Equal("Veri taşıma gecikir", closed.Title);
        Assert.Equal("Elif Yılmaz", closed.PerformedBy.FullName);
        Assert.False(closed.DateOnly);
    }

    [Fact]
    public async Task List_RiskClosedTwice_ShowsBothClosingsWithTheCurrentTitle()
    {
        var projectId = await TestProjects.CreateAsync(database, "7");
        var risk = await CreateRiskAsync(projectId);

        await UpdateRiskAsync(projectId, risk.Id, RiskStatus.Closed);
        // An edit that does not change the status is not a new closing.
        await UpdateRiskAsync(projectId, risk.Id, RiskStatus.Closed, "Veri taşıma iki hafta gecikir");
        await UpdateRiskAsync(projectId, risk.Id, RiskStatus.Open, "Veri taşıma iki hafta gecikir");
        await UpdateRiskAsync(projectId, risk.Id, RiskStatus.Closed, "Veri taşıma iki hafta gecikir");

        var closings = (await ListAsync(projectId)).Items.Where(e => e.Kind == TimelineEventKind.RiskClosed).ToList();

        Assert.Equal(2, closings.Count);
        Assert.All(closings, c => Assert.Equal("Veri taşıma iki hafta gecikir", c.Title));
        Assert.True(closings[0].OccurredAt >= closings[1].OccurredAt);
    }

    [Fact]
    public async Task List_RequirementApprovedAgainAfterReview_ShowsTwoApprovals()
    {
        var projectId = await TestProjects.CreateAsync(database, "7");
        var requirement = await InScopeAsync((db, userId) =>
            new RequirementService(db, new TestUser(userId)).CreateAsync(projectId, new CreateRequirementDto
            {
                Type = RequirementType.Functional, Title = "Raporu PDF al", Priority = RequirementPriority.ShouldHave,
            }, CancellationToken.None));

        await SetRequirementStatusAsync(projectId, requirement.Id, RequirementStatus.Approved);
        await SetRequirementStatusAsync(projectId, requirement.Id, RequirementStatus.InReview);
        await SetRequirementStatusAsync(projectId, requirement.Id, RequirementStatus.Approved);
        await SetRequirementStatusAsync(projectId, requirement.Id, RequirementStatus.Cancelled);

        var approvals = (await ListAsync(projectId)).Items.Where(e => e.Kind == TimelineEventKind.RequirementApproved).ToList();

        Assert.Equal(2, approvals.Count);
        Assert.All(approvals, a => Assert.Equal(requirement.Code, a.RecordCode));
    }

    [Fact]
    public async Task List_OnlyDecisionOpenIssuesAreEvents()
    {
        var projectId = await TestProjects.CreateAsync(database, "7");
        await CreateOpenIssueAsync(projectId, OpenIssueType.Question, "Raporlar hangi formatta?");
        await CreateOpenIssueAsync(projectId, OpenIssueType.Action, "Örnek rapor gönderilecek");
        var decision = await CreateOpenIssueAsync(projectId, OpenIssueType.Decision, "Raporlar Excel olarak alınacak");

        var items = (await ListAsync(projectId)).Items;

        var only = Assert.Single(items, e => e.Kind == TimelineEventKind.Decision);
        Assert.Equal(decision.Id, only.RecordId);
        Assert.Equal(decision.Code, only.RecordCode);
        Assert.Equal("Raporlar Excel olarak alınacak", only.Title);
        Assert.Equal(2, items.Count); // the decision and the project's creation
    }

    [Fact]
    public async Task List_AnswerNo_ShowsTheStepJumpedTo()
    {
        var projectId = await TestProjects.CreateAsync(database, "4");
        await InScopeAsync(async (db, userId) =>
        {
            db.StepRecords.Add(new StepRecord
            {
                ProjectId = projectId,
                ProcessStepId = await TestProjects.StepIdAsync(database, "4"),
                JumpedToStepId = await TestProjects.StepIdAsync(database, "3"),
                Action = StepAction.No,
                Note = "Eksik bilgi var.",
                PerformedById = userId,
                PerformedAt = DateTime.UtcNow,
            });
            return await db.SaveChangesAsync(CancellationToken.None);
        });

        var step = Assert.Single((await ListAsync(projectId)).Items, e => e.Kind == TimelineEventKind.Step);

        Assert.Equal(StepAction.No, step.StepAction);
        Assert.Equal("4", step.RecordCode);
        Assert.Equal("3", step.JumpedToStepCode);
        Assert.Equal("Eksik bilgi var.", step.Note);
    }

    [Fact]
    public async Task List_SameMoment_LaterKindFirstAndCreatedLast()
    {
        var projectId = await TestProjects.CreateAsync(database, "1");
        var createdAt = (await TestProjects.GetAsync(database, projectId)).CreatedAt;
        await AddStepRecordAsync(projectId, "1", StepAction.Completed, createdAt);

        var items = (await ListAsync(projectId)).Items;

        Assert.Equal([TimelineEventKind.Step, TimelineEventKind.Created], items.Select(e => e.Kind));
    }

    [Fact]
    public async Task List_AnotherProjectsRecords_AreNotShown()
    {
        var projectId = await TestProjects.CreateAsync(database, "7");
        var otherId = await TestProjects.CreateAsync(database, "7");
        var otherRisk = await CreateRiskAsync(otherId);
        await UpdateRiskAsync(otherId, otherRisk.Id, RiskStatus.Closed);
        await CreateOpenIssueAsync(otherId, OpenIssueType.Decision, "Başka projenin kararı");
        await AddStepRecordAsync(otherId, "1", StepAction.Completed, DateTime.UtcNow);

        var created = Assert.Single((await ListAsync(projectId)).Items);

        Assert.Equal(TimelineEventKind.Created, created.Kind);
    }
}
