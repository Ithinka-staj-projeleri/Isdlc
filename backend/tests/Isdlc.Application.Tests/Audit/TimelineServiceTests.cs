using Isdlc.Application.Audit;
using Isdlc.Application.Audit.Dtos;
using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Common.Models;
using Isdlc.Application.Meetings;
using Isdlc.Application.Meetings.Dtos;
using Isdlc.Application.Requirements;
using Isdlc.Application.Requirements.Dtos;
using Isdlc.Application.Tests.Common;
using Isdlc.Domain.Meetings;
using Isdlc.Domain.Process;
using Isdlc.Domain.Requirements;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Audit;

/// <summary>
/// A project's timeline on a real database. Records are made through the real services (so the audit log rows the
/// timeline reads are real), except step records, which are added directly. Expectations: docs/audit-api.md → Zaman çizelgesi.
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
}
