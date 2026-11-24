using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Common.Models;
using Isdlc.Application.Meetings;
using Isdlc.Application.Meetings.Dtos;
using Isdlc.Application.OpenIssues;
using Isdlc.Application.OpenIssues.Dtos;
using Isdlc.Application.Tests.Common;
using Isdlc.Domain.Meetings;
using Isdlc.Domain.OpenIssues;
using Isdlc.Domain.Tenants;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Tenants;

/// <summary>
/// Open issues and minutes across companies (Faz 6 · T7): Deneme cannot list, read or change Ithinka's, and cannot make
/// an Ithinka user the owner of an action or the writer of the minutes. Expectations: docs/multi-tenancy.md → Testler.
/// Cross test (PLANNING.md): Dide → Erva's modules.
/// </summary>
[Collection("Database")]
public class OpenIssueMeetingIsolationTests(DatabaseFixture database)
{
    private async Task<T> OpenIssuesAsync<T>(int tenantId, int userId, Func<OpenIssueService, Task<T>> action)
    {
        await using var scope = database.CreateScope(tenantId);
        var service = new OpenIssueService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(), new TestUser(userId, tenantId),
            new RecordingNotificationService(), TimeProvider.System);
        return await action(service);
    }

    private async Task<T> MeetingsAsync<T>(int tenantId, int userId, Func<MeetingService, Task<T>> action)
    {
        await using var scope = database.CreateScope(tenantId);
        var service = new MeetingService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(), new TestUser(userId, tenantId),
            new RecordingNotificationService(), TimeProvider.System);
        return await action(service);
    }

    private static CreateOpenIssueDto NewIssue(int ownerUserId) =>
        new() { Type = OpenIssueType.Action, Title = "Örnek rapor gönderilecek", OwnerUserId = ownerUserId };

    private static CreateMeetingDto NewMeeting(int recordedByUserId, int? actionOwnerUserId = null) => new()
    {
        Type = MeetingType.AnalysisSession,
        Date = new DateOnly(2026, 11, 20),
        RecordedByUserId = recordedByUserId,
        Participants = [new MeetingParticipantDto { Name = "Katılımcı" }],
        Agenda = "- Raporlar",
        Decisions = ["Raporlar PDF olarak da alınacak"],
        Actions = actionOwnerUserId is null
            ? []
            : [new MeetingActionDto { Title = "Örnek rapor", OwnerUserId = actionOwnerUserId }],
    };

    private async Task<(int IthinkaAnalystId, int DenemeAnalystId)> UsersAsync() =>
        (await TestProjects.UserIdAsync(database, TestProjects.Analyst),
            await TestProjects.UserIdAsync(database, TestTenants.DenemeAnalyst, database.DenemeId));

    // ---- Open issues ----

    [Fact]
    public async Task OpenIssue_OtherCompany_CannotListReadOrChangeIt()
    {
        var (ithinkaId, denemeId) = await UsersAsync();
        var projectId = await TestProjects.CreateAsync(database, "8");
        var issue = await OpenIssuesAsync(Tenant.IthinkaId, ithinkaId,
            s => s.CreateAsync(projectId, NewIssue(ithinkaId), CancellationToken.None));

        await Assert.ThrowsAsync<NotFoundException>(() => OpenIssuesAsync(database.DenemeId, denemeId,
            s => s.ListAsync(projectId, new OpenIssueFilter(), new PageRequest(), CancellationToken.None)));
        await Assert.ThrowsAsync<NotFoundException>(() => OpenIssuesAsync(database.DenemeId, denemeId,
            s => s.GetAsync(projectId, issue.Id, CancellationToken.None)));
        await Assert.ThrowsAsync<NotFoundException>(() => OpenIssuesAsync(database.DenemeId, denemeId,
            s => s.UpdateStatusAsync(projectId, issue.Id,
                new UpdateOpenIssueStatusDto { Status = OpenIssueStatus.Closed }, CancellationToken.None)));

        var unchanged = await OpenIssuesAsync(Tenant.IthinkaId, ithinkaId,
            s => s.GetAsync(projectId, issue.Id, CancellationToken.None));
        Assert.Equal(OpenIssueStatus.Open, unchanged.Status);
    }

    [Fact]
    public async Task OpenIssue_ThroughItsOwnProject_IsStillNotFound()
    {
        // Deneme's project id with Ithinka's record id: the record is not Deneme's, whatever the project.
        var (ithinkaId, denemeId) = await UsersAsync();
        var projectId = await TestProjects.CreateAsync(database, "8");
        var issue = await OpenIssuesAsync(Tenant.IthinkaId, ithinkaId,
            s => s.CreateAsync(projectId, NewIssue(ithinkaId), CancellationToken.None));
        var denemeProjectId = await TestProjects.CreateAsync(database, "8", tenantId: database.DenemeId);

        await Assert.ThrowsAsync<NotFoundException>(() => OpenIssuesAsync(database.DenemeId, denemeId,
            s => s.GetAsync(denemeProjectId, issue.Id, CancellationToken.None)));
    }

    [Fact]
    public async Task OpenIssue_OtherCompanysUser_CannotBeTheOwner()
    {
        var (ithinkaId, denemeId) = await UsersAsync();
        var denemeProjectId = await TestProjects.CreateAsync(database, "8", tenantId: database.DenemeId);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => OpenIssuesAsync(database.DenemeId, denemeId,
            s => s.CreateAsync(denemeProjectId, NewIssue(ithinkaId), CancellationToken.None)));

        Assert.Equal("Seçilen sorumlu aktif bir kullanıcı değil.", error.Message);
    }

    [Fact]
    public async Task OpenIssue_OwnCompanysUser_IsTheOwnerAndTheFirstCode()
    {
        var (_, denemeId) = await UsersAsync();
        var denemeProjectId = await TestProjects.CreateAsync(database, "8", tenantId: database.DenemeId);

        var issue = await OpenIssuesAsync(database.DenemeId, denemeId,
            s => s.CreateAsync(denemeProjectId, NewIssue(denemeId), CancellationToken.None));

        Assert.Equal("AK-001", issue.Code);
        Assert.Equal(denemeId, issue.Owner.UserId);
    }

    // ---- Minutes ----

    [Fact]
    public async Task Meeting_OtherCompany_CannotListReadOrChangeIt()
    {
        var (ithinkaId, denemeId) = await UsersAsync();
        var projectId = await TestProjects.CreateAsync(database, "8");
        var meeting = await MeetingsAsync(Tenant.IthinkaId, ithinkaId,
            s => s.CreateAsync(projectId, NewMeeting(ithinkaId), CancellationToken.None));

        await Assert.ThrowsAsync<NotFoundException>(() => MeetingsAsync(database.DenemeId, denemeId,
            s => s.ListAsync(projectId, new PageRequest(), CancellationToken.None)));
        await Assert.ThrowsAsync<NotFoundException>(() => MeetingsAsync(database.DenemeId, denemeId,
            s => s.GetAsync(projectId, meeting.Id, CancellationToken.None)));
        await Assert.ThrowsAsync<NotFoundException>(() => MeetingsAsync(database.DenemeId, denemeId,
            s => s.UpdateAsync(projectId, meeting.Id, NewMeeting(denemeId), CancellationToken.None)));
    }

    [Fact]
    public async Task Meeting_OtherCompanysUser_CannotHaveWrittenTheMinutes()
    {
        var (ithinkaId, denemeId) = await UsersAsync();
        var denemeProjectId = await TestProjects.CreateAsync(database, "8", tenantId: database.DenemeId);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => MeetingsAsync(database.DenemeId, denemeId,
            s => s.CreateAsync(denemeProjectId, NewMeeting(ithinkaId), CancellationToken.None)));

        Assert.Equal("Tutanağı yazan aktif bir kullanıcı olmalıdır.", error.Message);
    }

    [Fact]
    public async Task Meeting_OtherCompanysUser_CannotOwnAnAction()
    {
        var (ithinkaId, denemeId) = await UsersAsync();
        var denemeProjectId = await TestProjects.CreateAsync(database, "8", tenantId: database.DenemeId);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => MeetingsAsync(database.DenemeId, denemeId,
            s => s.CreateAsync(denemeProjectId, NewMeeting(denemeId, actionOwnerUserId: ithinkaId), CancellationToken.None)));

        Assert.Equal("1. aksiyon: Seçilen sorumlu aktif bir kullanıcı değil.", error.Message);
    }

    [Fact]
    public async Task Meeting_InOwnCompany_StartsItsOwnCodesAndItsRecordsStayInIt()
    {
        var (_, denemeId) = await UsersAsync();
        var denemeProjectId = await TestProjects.CreateAsync(database, "8", tenantId: database.DenemeId);

        var meeting = await MeetingsAsync(database.DenemeId, denemeId,
            s => s.CreateAsync(denemeProjectId, NewMeeting(denemeId, actionOwnerUserId: denemeId), CancellationToken.None));

        Assert.Equal("TT-001", meeting.Code);
        Assert.Equal(["AK-001"], meeting.Actions.Select(a => a.Code));
        Assert.Equal(["AK-002"], meeting.Decisions.Select(d => d.Code));
        Assert.Equal(denemeId, meeting.RecordedBy.Id);
    }
}
