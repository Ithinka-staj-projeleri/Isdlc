using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Common.Models;
using Isdlc.Application.Meetings;
using Isdlc.Application.Meetings.Dtos;
using Isdlc.Application.OpenIssues;
using Isdlc.Application.OpenIssues.Dtos;
using Isdlc.Application.Tests.Common;
using Isdlc.Application.Tests.Identity;
using Isdlc.Domain.Meetings;
using Isdlc.Domain.OpenIssues;
using Isdlc.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Meetings;

/// <summary>
/// Meeting minutes on a real database: decisions and actions become open issues of the project.
/// Every test creates its own project (TestProjects). Expectations: docs/open-issues-meetings-api.md
/// and docs/data-model.md → Meeting. Cross test (PLANNING.md): Dide → Erva's modules.
/// </summary>
[Collection("Database")]
public class MeetingServiceTests(DatabaseFixture database)
{
    private static readonly PageRequest FirstPage = new() { Page = 1, PageSize = 100 };
    private static readonly DateOnly MeetingDate = new(2026, 11, 2);

    private async Task<(T Result, RecordingNotificationService Notifications)> AsAsync<T>(
        string? email, Func<IMeetingService, Task<T>> action)
    {
        var userId = email is null ? (int?)null : await TestProjects.UserIdAsync(database, email);
        await using var scope = database.CreateScope();
        var notifications = new RecordingNotificationService();
        var service = new MeetingService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
            new TestUser(userId),
            notifications,
            TimeProvider.System);
        return (await action(service), notifications);
    }

    private async Task<MeetingDto> CreateAsync(int projectId, CreateMeetingDto request) =>
        (await AsAsync(TestProjects.Analyst, s => s.CreateAsync(projectId, request, CancellationToken.None))).Result;

    /// <summary>Who writes the minutes in these tests (the seed's business analyst).</summary>
    private readonly int _analystId = TestProjects.UserIdAsync(database, TestProjects.Analyst).GetAwaiter().GetResult();

    private CreateMeetingDto NewMeeting(
        IReadOnlyList<string?>? decisions = null, IReadOnlyList<MeetingActionDto>? actions = null,
        DateOnly? date = null) => new()
    {
        Type = MeetingType.AnalysisSession,
        Date = date ?? MeetingDate,
        RecordedByUserId = _analystId,
        Participants =
        [
            new MeetingParticipantDto { Name = "  Elif Yılmaz  ", Role = " İş Analisti " },
            new MeetingParticipantDto { Name = "Can Öztürk" },
            new MeetingParticipantDto { Name = "Müşteri BT", Role = "   " },
        ],
        Agenda = "  ERP servisleri ve iş emri yaşam döngüsü  ",
        Decisions = decisions,
        Actions = actions,
    };

    private static MeetingActionDto Action(string title, int? ownerUserId = null, string? ownerName = "Müşteri BT",
        DateOnly? dueDate = null) =>
        new() { Title = title, OwnerUserId = ownerUserId, OwnerName = ownerName, DueDate = dueDate };

    private async Task<List<OpenIssue>> IssuesAsync(int projectId)
    {
        await using var scope = database.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IApplicationDbContext>().OpenIssues
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderBy(x => x.Code)
            .ToListAsync();
    }

    private async Task<int> MeetingCountAsync(int projectId)
    {
        await using var scope = database.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IApplicationDbContext>().Meetings
            .CountAsync(m => m.ProjectId == projectId);
    }

    // ---- Create: the minutes ----

    [Fact]
    public async Task Create_FirstMeeting_GetsFirstCodeAndTrimmedText()
    {
        var projectId = await TestProjects.CreateAsync(database, "14");

        var meeting = await CreateAsync(projectId, NewMeeting());

        Assert.Equal("TT-001", meeting.Code);
        Assert.Equal(MeetingType.AnalysisSession, meeting.Type);
        Assert.Equal(MeetingDate, meeting.Date);
        Assert.Equal(
            [("Elif Yılmaz", "İş Analisti"), ("Can Öztürk", null), ("Müşteri BT", null)],
            meeting.Participants.Select(p => (p.Name, p.Role)));
        Assert.Equal("Elif Yılmaz", meeting.RecordedBy.FullName);
        Assert.Equal("ERP servisleri ve iş emri yaşam döngüsü", meeting.Agenda);
        Assert.Empty(meeting.Decisions);
        Assert.Empty(meeting.Actions);
    }

    [Fact]
    public async Task Create_SecondMeeting_GetsNextCode()
    {
        var projectId = await TestProjects.CreateAsync(database, "14");

        await CreateAsync(projectId, NewMeeting());
        var second = await CreateAsync(projectId, NewMeeting());

        Assert.Equal("TT-002", second.Code);
    }

    // ---- Create: decisions and actions become open issues ----

    [Fact]
    public async Task Create_ActionsBecomeOpenActions_DecisionsBecomeClosedDecisions()
    {
        var projectId = await TestProjects.CreateAsync(database, "14");
        var analystId = await TestProjects.UserIdAsync(database, TestProjects.Analyst);
        var developerId = await TestProjects.UserIdAsync(database, TestProjects.Developer);

        var meeting = await CreateAsync(projectId, NewMeeting(
            decisions: ["İş emri durumları 6 adım olarak kesinleşti."],
            actions:
            [
                Action("ERP test ortamı erişimi", ownerName: "  Müşteri BT  ", dueDate: MeetingDate.AddDays(7)),
                Action("İş emri ekran taslakları", ownerUserId: developerId, ownerName: null),
            ]));

        var issues = await IssuesAsync(projectId);
        Assert.Equal(3, issues.Count);
        Assert.All(issues, x => Assert.Equal(meeting.Id, x.SourceMeetingId));

        var actions = issues.Where(x => x.Type == OpenIssueType.Action).ToList();
        Assert.Equal(2, actions.Count);
        Assert.All(actions, x =>
        {
            Assert.Equal(OpenIssueStatus.Open, x.Status);
            Assert.Null(x.ClosedAt);
        });
        Assert.Equal("Müşteri BT", actions[0].OwnerName);
        Assert.Equal(MeetingDate.AddDays(7), actions[0].DueDate);
        Assert.Equal(developerId, actions[1].OwnerUserId);
        Assert.Null(actions[1].OwnerName);

        // A decision is settled: closed, owned by whoever saved the minutes, due on the meeting date.
        var decision = Assert.Single(issues, x => x.Type == OpenIssueType.Decision);
        Assert.Equal(OpenIssueStatus.Closed, decision.Status);
        Assert.NotNull(decision.ClosedAt);
        Assert.Equal(analystId, decision.OwnerUserId);
        Assert.Equal(MeetingDate, decision.DueDate);
        Assert.Equal("İş emri durumları 6 adım olarak kesinleşti.", decision.Title);
    }

    [Fact]
    public async Task Create_IssueCodes_ContinueTheProjectList_ActionsFirstThenDecisions()
    {
        var projectId = await TestProjects.CreateAsync(database, "14");
        await using (var scope = database.CreateScope())
        {
            // An open issue added directly before the meeting: AK-001.
            await new OpenIssueService(
                    scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
                    new TestUser(await TestProjects.UserIdAsync(database, TestProjects.Analyst)),
                    new RecordingNotificationService(),
                    TimeProvider.System)
                .CreateAsync(projectId, new CreateOpenIssueDto
                {
                    Type = OpenIssueType.Question, Title = "Önceki soru", OwnerName = "Müşteri BT",
                }, CancellationToken.None);
        }

        var meeting = await CreateAsync(projectId, NewMeeting(
            decisions: ["Birinci karar", "İkinci karar"],
            actions: [Action("Birinci aksiyon"), Action("İkinci aksiyon")]));

        Assert.Equal(["AK-002", "AK-003"], meeting.Actions.Select(x => x.Code));
        Assert.Equal(["Birinci aksiyon", "İkinci aksiyon"], meeting.Actions.Select(x => x.Title));
        Assert.Equal(["AK-004", "AK-005"], meeting.Decisions.Select(x => x.Code));
        Assert.Equal(["Birinci karar", "İkinci karar"], meeting.Decisions.Select(x => x.Title));
        Assert.All(meeting.Actions.Concat(meeting.Decisions), x => Assert.Equal(meeting.Code, x.SourceMeeting!.Code));
    }

    [Fact]
    public async Task Create_BlankDecisionLines_AreSkipped()
    {
        var projectId = await TestProjects.CreateAsync(database, "14");

        var meeting = await CreateAsync(projectId, NewMeeting(decisions: ["", "   ", null, "  Tek karar  "]));

        Assert.Equal("Tek karar", Assert.Single(meeting.Decisions).Title);
    }

    // ---- Create: one wrong line saves nothing ----

    [Fact]
    public async Task Create_SecondActionHasNoOwner_SavesNothingAndNamesTheLine()
    {
        var projectId = await TestProjects.CreateAsync(database, "14");

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateAsync(projectId, NewMeeting(
            decisions: ["Karar"],
            actions: [Action("Doğru aksiyon"), Action("Sorumlusuz aksiyon", ownerName: null)])));

        Assert.StartsWith("2. aksiyon: ", error.Message);
        Assert.Equal(0, await MeetingCountAsync(projectId));
        Assert.Empty(await IssuesAsync(projectId));
    }

    [Fact]
    public async Task Create_DecisionTooLong_SavesNothingAndNamesTheLine()
    {
        var projectId = await TestProjects.CreateAsync(database, "14");

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateAsync(projectId, NewMeeting(
            decisions: ["Kısa karar", "", new string('k', 301)],
            actions: [Action("Aksiyon")])));

        Assert.Equal("3. karar en fazla 300 karakter olabilir.", error.Message);
        Assert.Equal(0, await MeetingCountAsync(projectId));
        Assert.Empty(await IssuesAsync(projectId));
    }

    [Fact]
    public async Task Create_ProjectNotActive_ThrowsConflict()
    {
        var projectId = await TestProjects.CreateAsync(database, null, ProjectStatus.Rejected);

        await Assert.ThrowsAsync<ConflictException>(() => CreateAsync(projectId, NewMeeting()));
    }

    [Fact]
    public async Task Create_NotSignedIn_ThrowsForbidden()
    {
        var projectId = await TestProjects.CreateAsync(database, "14");

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            AsAsync(null, s => s.CreateAsync(projectId, NewMeeting(), CancellationToken.None)));
    }

    // ---- Notifications ----

    [Fact]
    public async Task Create_ActionAssignedToTeamMember_NotifiesOnlyThem()
    {
        var projectId = await TestProjects.CreateAsync(database, "14");
        var analystId = await TestProjects.UserIdAsync(database, TestProjects.Analyst);
        var developerId = await TestProjects.UserIdAsync(database, TestProjects.Developer);

        var (_, notifications) = await AsAsync(TestProjects.Analyst, s => s.CreateAsync(projectId, NewMeeting(
            decisions: ["Karar"],
            actions:
            [
                Action("Geliştiriciye", ownerUserId: developerId, ownerName: null),
                Action("Kendine", ownerUserId: analystId, ownerName: null),
                Action("Dışarıya"),
            ]), CancellationToken.None));

        // Not the decision (owned by the saver), not the saver's own action, not the outsider.
        Assert.Equal([developerId], Assert.Single(notifications.Sent).UserIds);
    }

    // ---- Get and list ----

    [Fact]
    public async Task Get_ShowsTheCurrentStatusOfActions()
    {
        var projectId = await TestProjects.CreateAsync(database, "14");
        var meeting = await CreateAsync(projectId, NewMeeting(actions: [Action("Kapanacak aksiyon")]));
        var actionId = Assert.Single(meeting.Actions).Id;
        await using (var scope = database.CreateScope())
        {
            await new OpenIssueService(
                    scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
                    new TestUser(await TestProjects.UserIdAsync(database, TestProjects.Analyst)),
                    new RecordingNotificationService(),
                    TimeProvider.System)
                .UpdateStatusAsync(projectId, actionId,
                    new UpdateOpenIssueStatusDto { Status = OpenIssueStatus.Closed }, CancellationToken.None);
        }

        var (reloaded, _) = await AsAsync(TestProjects.Sales, s =>
            s.GetAsync(projectId, meeting.Id, CancellationToken.None));

        Assert.Equal(OpenIssueStatus.Closed, Assert.Single(reloaded.Actions).Status);
    }

    [Fact]
    public async Task Get_MeetingOfAnotherProject_ThrowsNotFound()
    {
        var projectId = await TestProjects.CreateAsync(database, "14");
        var otherProjectId = await TestProjects.CreateAsync(database, "14");
        var meeting = await CreateAsync(projectId, NewMeeting());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            AsAsync(TestProjects.Analyst, s => s.GetAsync(otherProjectId, meeting.Id, CancellationToken.None)));
    }

    [Fact]
    public async Task List_NewestFirst_WithDecisionAndActionCounts()
    {
        var projectId = await TestProjects.CreateAsync(database, "14");
        var older = await CreateAsync(projectId, NewMeeting(date: MeetingDate.AddDays(-7)));
        var newer = await CreateAsync(projectId, NewMeeting(
            decisions: ["Karar 1", "Karar 2"],
            actions: [Action("Açık kalacak"), Action("Kapanacak")]));
        await using (var scope = database.CreateScope())
        {
            await new OpenIssueService(
                    scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
                    new TestUser(await TestProjects.UserIdAsync(database, TestProjects.Analyst)),
                    new RecordingNotificationService(),
                    TimeProvider.System)
                .UpdateStatusAsync(projectId, newer.Actions[1].Id,
                    new UpdateOpenIssueStatusDto { Status = OpenIssueStatus.Closed }, CancellationToken.None);
        }

        var (result, _) = await AsAsync(TestProjects.Sales, s =>
            s.ListAsync(projectId, FirstPage, CancellationToken.None));

        Assert.Equal([newer.Code, older.Code], result.Items.Select(x => x.Code));
        var row = result.Items[0];
        Assert.Equal(2, row.DecisionCount);
        Assert.Equal(2, row.ActionCount);
        Assert.Equal(1, row.OpenActionCount);
    }

    // ---- Update ----

    [Fact]
    public async Task Update_ChangesTheMinutesOnly_KeepsDecisionsAndActions()
    {
        var projectId = await TestProjects.CreateAsync(database, "14");
        var meeting = await CreateAsync(projectId, NewMeeting(decisions: ["Karar"], actions: [Action("Aksiyon")]));

        var (updated, _) = await AsAsync(TestProjects.ProjectManager, s => s.UpdateAsync(projectId, meeting.Id,
            new UpdateMeetingDto
            {
                Type = MeetingType.Review,
                Date = MeetingDate.AddDays(1),
                RecordedByUserId = _analystId,
                Participants = [new MeetingParticipantDto { Name = "Ayşe Demir" }],
                Agenda = "Gözden geçirme",
            }, CancellationToken.None));

        Assert.Equal(meeting.Code, updated.Code);
        Assert.Equal(MeetingType.Review, updated.Type);
        Assert.Equal(MeetingDate.AddDays(1), updated.Date);
        Assert.Equal("Ayşe Demir", Assert.Single(updated.Participants).Name);
        Assert.Single(updated.Decisions);
        Assert.Single(updated.Actions);
        Assert.NotNull(updated.UpdatedAt);
    }

    // ---- Faz 4 · E4: meeting and visit fields ----

    private CreateMeetingDto NewVisit() => NewMeeting() with
    {
        Type = MeetingType.CustomerVisit,
        Date = MeetingDate,
        ReturnDate = MeetingDate.AddDays(1),
        Agenda = "Depoda sayım sürecinin incelenmesi",
        Summary = "  Sayım kâğıtla yapılıyor.  ",
        CompletedStages = " Talep ve ön analiz ",
        OngoingStages = "Teklif ve anlaşma",
        NextSteps = "Ekran taslakları gözden geçirilecek.",
        PlannedActivities = "- Sayımı izleme",
        CompletedActivities = "- Sayım izlendi ",
    };

    private UpdateMeetingDto AsUpdate(CreateMeetingDto create) => new()
    {
        Type = create.Type, Date = create.Date, ReturnDate = create.ReturnDate, RecordedByUserId = create.RecordedByUserId,
        Participants = create.Participants, Agenda = create.Agenda, Summary = create.Summary,
        CompletedStages = create.CompletedStages, OngoingStages = create.OngoingStages, NextSteps = create.NextSteps,
        PlannedActivities = create.PlannedActivities, CompletedActivities = create.CompletedActivities,
    };

    private async Task<MeetingDto> UpdateAsync(int projectId, int meetingId, UpdateMeetingDto request) =>
        (await AsAsync(TestProjects.Analyst, s => s.UpdateAsync(projectId, meetingId, request, CancellationToken.None))).Result;

    [Fact]
    public async Task Create_Visit_KeepsTheVisitAndMinutesFields_Trimmed()
    {
        var projectId = await TestProjects.CreateAsync(database, "14");

        var visit = await CreateAsync(projectId, NewVisit());

        Assert.Equal(MeetingType.CustomerVisit, visit.Type);
        Assert.Equal(MeetingDate.AddDays(1), visit.ReturnDate);
        Assert.Equal("Sayım kâğıtla yapılıyor.", visit.Summary);
        Assert.Equal("Talep ve ön analiz", visit.CompletedStages);
        Assert.Equal("Teklif ve anlaşma", visit.OngoingStages);
        Assert.Equal("Ekran taslakları gözden geçirilecek.", visit.NextSteps);
        Assert.Equal("- Sayımı izleme", visit.PlannedActivities);
        Assert.Equal("- Sayım izlendi", visit.CompletedActivities);
    }

    [Fact]
    public async Task Create_BlankOptionalTexts_AreNotKept()
    {
        var projectId = await TestProjects.CreateAsync(database, "14");

        var meeting = await CreateAsync(projectId, NewMeeting() with { Summary = "  ", NextSteps = "", CompletedStages = null });

        Assert.Null(meeting.Summary);
        Assert.Null(meeting.NextSteps);
        Assert.Null(meeting.CompletedStages);
        Assert.Null(meeting.ReturnDate);
    }

    [Fact]
    public async Task List_ShowsParticipantNamesAndTheVisitsReturnDate()
    {
        var projectId = await TestProjects.CreateAsync(database, "14");
        await CreateAsync(projectId, NewVisit());

        var (page, _) = await AsAsync(TestProjects.Analyst, s => s.ListAsync(projectId, new PageRequest(), CancellationToken.None));

        var row = Assert.Single(page.Items);
        Assert.Equal("Elif Yılmaz, Can Öztürk, Müşteri BT", row.ParticipantNames);
        Assert.Equal(MeetingDate.AddDays(1), row.ReturnDate);
    }

    [Fact]
    public async Task Create_VisitFieldsOnAnotherType_Throws400_AndSavesNothing()
    {
        var projectId = await TestProjects.CreateAsync(database, "14");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CreateAsync(projectId, NewVisit() with { Type = MeetingType.Review, Actions = [Action("Aksiyon")] }));

        Assert.Equal(MeetingRules.VisitFieldsOnlyOnVisit, ex.Message);
        Assert.Equal(0, await MeetingCountAsync(projectId));
        Assert.Empty(await IssuesAsync(projectId));
    }

    [Fact]
    public async Task Create_VisitReturningBeforeDeparture_Throws400()
    {
        var projectId = await TestProjects.CreateAsync(database, "14");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CreateAsync(projectId, NewVisit() with { ReturnDate = MeetingDate.AddDays(-1) }));

        Assert.Equal(MeetingRules.ReturnBeforeDeparture, ex.Message);
    }

    [Fact]
    public async Task Create_InactiveOrUnknownRecorder_Throws400()
    {
        var projectId = await TestProjects.CreateAsync(database, "14");
        var inactive = await TestUsers.CreateAsync(database, password: null, isActive: false);

        foreach (var recorderId in new[] { inactive.Id, int.MaxValue })
        {
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                CreateAsync(projectId, NewMeeting() with { RecordedByUserId = recorderId }));
            Assert.Equal("Tutanağı yazan aktif bir kullanıcı olmalıdır.", ex.Message);
        }
    }

    [Fact]
    public async Task Create_RecordedByAnotherTeamMember_ShowsThem()
    {
        var projectId = await TestProjects.CreateAsync(database, "14");
        var developerId = await TestProjects.UserIdAsync(database, TestProjects.Developer);

        var meeting = await CreateAsync(projectId, NewMeeting() with { RecordedByUserId = developerId });

        Assert.Equal("Can Öztürk", meeting.RecordedBy.FullName);
        Assert.Equal("Elif Yılmaz", meeting.CreatedBy.FullName);
    }

    [Fact]
    public async Task Update_VisitChangedToReview_ClearsTheVisitFields_KeepsTheMinutesFields()
    {
        var projectId = await TestProjects.CreateAsync(database, "14");
        var visit = await CreateAsync(projectId, NewVisit());

        var review = await UpdateAsync(projectId, visit.Id, AsUpdate(NewVisit()) with
        {
            Type = MeetingType.Review, ReturnDate = null, PlannedActivities = null, CompletedActivities = null,
        });

        Assert.Equal(MeetingType.Review, review.Type);
        Assert.Null(review.ReturnDate);
        Assert.Null(review.PlannedActivities);
        Assert.Null(review.CompletedActivities);
        Assert.Equal("Sayım kâğıtla yapılıyor.", review.Summary);
    }

    [Fact]
    public async Task Update_ParticipantsChanged_IsLoggedAsOneField_SameListAgainIsNotLogged()
    {
        var projectId = await TestProjects.CreateAsync(database, "14");
        var meeting = await CreateAsync(projectId, NewMeeting());
        var update = AsUpdate(NewMeeting());

        await UpdateAsync(projectId, meeting.Id, update);  // same values: nothing to log
        await UpdateAsync(projectId, meeting.Id, update with
        {
            Participants = [.. update.Participants!, new MeetingParticipantDto { Name = "Hakan Yıldız", Role = "Depo Müdürü" }],
        });

        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var logs = await db.AuditLogs
            .Where(l => l.EntityName == nameof(Meeting) && l.EntityId == meeting.Id)
            .OrderBy(l => l.Id)
            .ToListAsync();

        Assert.Equal(2, logs.Count);  // created, then the one real change
        Assert.Contains("Participants", logs[1].Changes);
        Assert.Contains("Hakan Yıldız", logs[1].Changes);
        Assert.DoesNotContain("Agenda", logs[1].Changes);
    }
}
