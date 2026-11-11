using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Common.Models;
using Isdlc.Application.OpenIssues;
using Isdlc.Application.OpenIssues.Dtos;
using Isdlc.Application.Tests.Common;
using Isdlc.Domain.Audit;
using Isdlc.Domain.Identity;
using Isdlc.Domain.OpenIssues;
using Isdlc.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.OpenIssues;

/// <summary>
/// The Open Issues List on a real database. Every test creates its own project (TestProjects),
/// so seed projects are never changed. Expectations: docs/open-issues-meetings-api.md.
/// Cross test (PLANNING.md): Dide → Erva's modules.
/// </summary>
[Collection("Database")]
public class OpenIssueServiceTests(DatabaseFixture database)
{
    private static readonly PageRequest FirstPage = new() { Page = 1, PageSize = 100 };
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>The service as one user would call it, with notifications kept for checking.</summary>
    private async Task<(T Result, RecordingNotificationService Notifications)> AsAsync<T>(
        string? email, Func<IOpenIssueService, Task<T>> action)
    {
        var userId = email is null ? (int?)null : await TestProjects.UserIdAsync(database, email);
        await using var scope = database.CreateScope();
        var notifications = new RecordingNotificationService();
        var service = new OpenIssueService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
            new TestUser(userId),
            notifications,
            TimeProvider.System);
        return (await action(service), notifications);
    }

    private static CreateOpenIssueDto NewIssue(
        string title = "UAT için anahtar kullanıcı listesi",
        int? ownerUserId = null,
        string? ownerName = "Müşteri BT",
        DateOnly? dueDate = null,
        OpenIssueType type = OpenIssueType.Action) => new()
    {
        Type = type,
        Title = title,
        Description = null,
        OwnerUserId = ownerUserId,
        OwnerName = ownerName,
        DueDate = dueDate,
    };

    private async Task<OpenIssueDto> CreateAsync(int projectId, CreateOpenIssueDto request) =>
        (await AsAsync(TestProjects.Analyst, s => s.CreateAsync(projectId, request, CancellationToken.None))).Result;

    private async Task<OpenIssueDto> SetStatusAsync(int projectId, int openIssueId, OpenIssueStatus status) =>
        (await AsAsync(TestProjects.Analyst, s => s.UpdateStatusAsync(
            projectId, openIssueId, new UpdateOpenIssueStatusDto { Status = status }, CancellationToken.None))).Result;

    private async Task<PagedResult<OpenIssueListItemDto>> ListAsync(int projectId, OpenIssueFilter? filter = null) =>
        (await AsAsync(TestProjects.Sales, s => s.ListAsync(
            projectId, filter ?? new OpenIssueFilter(), FirstPage, CancellationToken.None))).Result;

    /// <summary>A user of this test only, so seed users stay as they are.</summary>
    private async Task<int> CreateInactiveUserAsync()
    {
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var user = new User
        {
            FullName = "Pasif Kullanıcı",
            Email = $"pasif-{Guid.NewGuid():N}@isdlc.local",
            Role = UserRole.SoftwareDeveloper,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    // ---- Create ----

    [Fact]
    public async Task Create_FirstRecord_StartsOpenWithFirstCode()
    {
        var projectId = await TestProjects.CreateAsync(database, "12");

        var created = await CreateAsync(projectId, NewIssue(ownerName: "  Müşteri BT  "));

        Assert.Equal("AK-001", created.Code);
        Assert.Equal(OpenIssueStatus.Open, created.Status);
        Assert.Null(created.ClosedAt);
        Assert.Equal(new OwnerDto(null, "Müşteri BT"), created.Owner);
        Assert.Null(created.SourceMeeting);
    }

    [Fact]
    public async Task Create_NextRecords_GetIncreasingCodesPerProject()
    {
        var projectId = await TestProjects.CreateAsync(database, "12");
        var otherProjectId = await TestProjects.CreateAsync(database, "12");

        await CreateAsync(projectId, NewIssue());
        var second = await CreateAsync(projectId, NewIssue());
        var otherFirst = await CreateAsync(otherProjectId, NewIssue());

        Assert.Equal("AK-002", second.Code);
        Assert.Equal("AK-001", otherFirst.Code);
    }

    [Fact]
    public async Task Create_OwnerUser_ShowsUserNameAndIgnoresOwnerName()
    {
        var projectId = await TestProjects.CreateAsync(database, "12");
        var developerId = await TestProjects.UserIdAsync(database, TestProjects.Developer);

        var created = await CreateAsync(projectId, NewIssue(ownerUserId: developerId, ownerName: null));

        Assert.Equal(new OwnerDto(developerId, "Can Öztürk"), created.Owner);
    }

    [Theory]
    [InlineData(true, "Müşteri BT")] // both
    [InlineData(false, null)]        // none
    [InlineData(false, "   ")]       // blank name counts as none
    public async Task Create_NotExactlyOneOwner_ThrowsBusinessRule(bool withUser, string? ownerName)
    {
        var projectId = await TestProjects.CreateAsync(database, "12");
        int? ownerUserId = withUser ? await TestProjects.UserIdAsync(database, TestProjects.Developer) : null;

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CreateAsync(projectId, NewIssue(ownerUserId: ownerUserId, ownerName: ownerName)));
    }

    [Fact]
    public async Task Create_InactiveOwner_ThrowsBusinessRule()
    {
        var projectId = await TestProjects.CreateAsync(database, "12");
        var inactiveUserId = await CreateInactiveUserAsync();

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CreateAsync(projectId, NewIssue(ownerUserId: inactiveUserId, ownerName: null)));

        Assert.Equal("Seçilen sorumlu aktif bir kullanıcı değil.", error.Message);
    }

    [Fact]
    public async Task Create_ProjectNotActive_ThrowsConflict()
    {
        var projectId = await TestProjects.CreateAsync(database, null, ProjectStatus.Closed);

        await Assert.ThrowsAsync<ConflictException>(() => CreateAsync(projectId, NewIssue()));
    }

    [Fact]
    public async Task Create_NotSignedIn_ThrowsForbidden()
    {
        var projectId = await TestProjects.CreateAsync(database, "12");

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            AsAsync(null, s => s.CreateAsync(projectId, NewIssue(), CancellationToken.None)));
    }

    [Fact]
    public async Task Create_WritesAuditLogWithProject()
    {
        var projectId = await TestProjects.CreateAsync(database, "12");

        var created = await CreateAsync(projectId, NewIssue());

        await using var scope = database.CreateScope();
        var log = await scope.ServiceProvider.GetRequiredService<IApplicationDbContext>().AuditLogs
            .AsNoTracking()
            .SingleAsync(l => l.EntityName == nameof(OpenIssue) && l.EntityId == created.Id);
        Assert.Equal(projectId, log.ProjectId);
        Assert.Equal(AuditAction.Created, log.Action);
    }

    // ---- Notifications ----

    [Fact]
    public async Task Create_AssignedToSomeoneElse_NotifiesOwner()
    {
        var projectId = await TestProjects.CreateAsync(database, "12");
        var developerId = await TestProjects.UserIdAsync(database, TestProjects.Developer);

        var (created, notifications) = await AsAsync(TestProjects.Analyst, s => s.CreateAsync(
            projectId, NewIssue(ownerUserId: developerId, ownerName: null), CancellationToken.None));

        var sent = Assert.Single(notifications.Sent);
        Assert.Equal([developerId], sent.UserIds);
        Assert.Equal(projectId, sent.ProjectId);
        Assert.Contains(created.Code, sent.Message);
    }

    [Fact]
    public async Task Create_AssignedToSelfOrOutsider_SendsNothing()
    {
        var projectId = await TestProjects.CreateAsync(database, "12");
        var analystId = await TestProjects.UserIdAsync(database, TestProjects.Analyst);

        var (_, toSelf) = await AsAsync(TestProjects.Analyst, s => s.CreateAsync(
            projectId, NewIssue(ownerUserId: analystId, ownerName: null), CancellationToken.None));
        var (_, toOutsider) = await AsAsync(TestProjects.Analyst, s => s.CreateAsync(
            projectId, NewIssue(ownerName: "Müşteri BT"), CancellationToken.None));

        Assert.Empty(toSelf.Sent);
        Assert.Empty(toOutsider.Sent);
    }

    [Fact]
    public async Task Update_OwnerChanged_NotifiesOnlyTheNewOwner()
    {
        var projectId = await TestProjects.CreateAsync(database, "12");
        var developerId = await TestProjects.UserIdAsync(database, TestProjects.Developer);
        var managerId = await TestProjects.UserIdAsync(database, TestProjects.ProjectManager);
        var created = await CreateAsync(projectId, NewIssue(ownerUserId: developerId, ownerName: null));

        var (_, sameOwner) = await AsAsync(TestProjects.Analyst, s => s.UpdateAsync(projectId, created.Id,
            new UpdateOpenIssueDto { Type = OpenIssueType.Action, Title = "Yeni konu", OwnerUserId = developerId },
            CancellationToken.None));
        var (_, newOwner) = await AsAsync(TestProjects.Analyst, s => s.UpdateAsync(projectId, created.Id,
            new UpdateOpenIssueDto { Type = OpenIssueType.Action, Title = "Yeni konu", OwnerUserId = managerId },
            CancellationToken.None));

        Assert.Empty(sameOwner.Sent);
        Assert.Equal([managerId], Assert.Single(newOwner.Sent).UserIds);
    }

    // ---- Update ----

    [Fact]
    public async Task Update_ChangesContent_KeepsCodeAndStatus()
    {
        var projectId = await TestProjects.CreateAsync(database, "12");
        var created = await CreateAsync(projectId, NewIssue());
        await SetStatusAsync(projectId, created.Id, OpenIssueStatus.InProgress);

        var (updated, _) = await AsAsync(TestProjects.ProjectManager, s => s.UpdateAsync(projectId, created.Id,
            new UpdateOpenIssueDto
            {
                Type = OpenIssueType.Dependency,
                Title = "  ERP test ortamı erişimi  ",
                Description = "  Müşteri BT'den bekleniyor.  ",
                OwnerName = "Müşteri BT",
                DueDate = Today.AddDays(5),
            }, CancellationToken.None));

        Assert.Equal(created.Code, updated.Code);
        Assert.Equal(OpenIssueStatus.InProgress, updated.Status);
        Assert.Equal(OpenIssueType.Dependency, updated.Type);
        Assert.Equal("ERP test ortamı erişimi", updated.Title);
        Assert.Equal("Müşteri BT'den bekleniyor.", updated.Description);
        Assert.Equal(Today.AddDays(5), updated.DueDate);
        Assert.NotNull(updated.UpdatedAt);
    }

    [Fact]
    public async Task Update_RecordOfAnotherProject_ThrowsNotFound()
    {
        var projectId = await TestProjects.CreateAsync(database, "12");
        var otherProjectId = await TestProjects.CreateAsync(database, "12");
        var created = await CreateAsync(projectId, NewIssue());

        await Assert.ThrowsAsync<NotFoundException>(() => AsAsync(TestProjects.Analyst, s => s.UpdateAsync(
            otherProjectId, created.Id,
            new UpdateOpenIssueDto { Type = OpenIssueType.Action, Title = "Başka proje", OwnerName = "Müşteri BT" },
            CancellationToken.None)));
    }

    [Fact]
    public async Task Get_RecordOfAnotherProject_ThrowsNotFound()
    {
        var projectId = await TestProjects.CreateAsync(database, "12");
        var otherProjectId = await TestProjects.CreateAsync(database, "12");
        var created = await CreateAsync(projectId, NewIssue());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            AsAsync(TestProjects.Analyst, s => s.GetAsync(otherProjectId, created.Id, CancellationToken.None)));
    }

    // ---- Status ----

    [Fact]
    public async Task UpdateStatus_CloseThenReopen_SetsAndClearsClosedAt()
    {
        var projectId = await TestProjects.CreateAsync(database, "12");
        var created = await CreateAsync(projectId, NewIssue());

        var closed = await SetStatusAsync(projectId, created.Id, OpenIssueStatus.Closed);
        var reopened = await SetStatusAsync(projectId, created.Id, OpenIssueStatus.Open);

        Assert.Equal(OpenIssueStatus.Closed, closed.Status);
        Assert.NotNull(closed.ClosedAt);
        Assert.Equal(OpenIssueStatus.Open, reopened.Status);
        Assert.Null(reopened.ClosedAt);
    }

    [Fact]
    public async Task UpdateStatus_ProjectClosedLater_ThrowsConflict()
    {
        var projectId = await TestProjects.CreateAsync(database, "12");
        var created = await CreateAsync(projectId, NewIssue());
        await using (var scope = database.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var project = await db.Projects.SingleAsync(p => p.Id == projectId);
            project.Status = ProjectStatus.Lost;
            project.CurrentStepId = null;
            await db.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<ConflictException>(() =>
            SetStatusAsync(projectId, created.Id, OpenIssueStatus.Closed));

        // Still readable.
        Assert.Equal(1, (await ListAsync(projectId)).TotalCount);
    }

    // ---- Overdue ----

    [Fact]
    public async Task IsOverdue_PastDueAndNotClosed_IsTrue()
    {
        var projectId = await TestProjects.CreateAsync(database, "12");
        var late = await CreateAsync(projectId, NewIssue("Gecikmiş", dueDate: Today.AddDays(-1)));
        var dueToday = await CreateAsync(projectId, NewIssue("Bugün", dueDate: Today));
        var closedLate = await CreateAsync(projectId, NewIssue("Kapanmış", dueDate: Today.AddDays(-3)));
        closedLate = await SetStatusAsync(projectId, closedLate.Id, OpenIssueStatus.Closed);

        Assert.True(late.IsOverdue);
        Assert.False(dueToday.IsOverdue);
        Assert.False(closedLate.IsOverdue);
    }

    // ---- List ----

    [Fact]
    public async Task List_NotClosedFirst_ThenDueDateWithEmptyLast_ThenCode()
    {
        var projectId = await TestProjects.CreateAsync(database, "12");
        var noDue = await CreateAsync(projectId, NewIssue("Termini yok"));                       // AK-001
        var later = await CreateAsync(projectId, NewIssue("Sonra", dueDate: Today.AddDays(10)));  // AK-002
        var closed = await CreateAsync(projectId, NewIssue("Kapalı", dueDate: Today.AddDays(1))); // AK-003
        var sooner = await CreateAsync(projectId, NewIssue("Önce", dueDate: Today.AddDays(2)));   // AK-004
        await SetStatusAsync(projectId, closed.Id, OpenIssueStatus.Closed);

        var result = await ListAsync(projectId);

        Assert.Equal([sooner.Code, later.Code, noDue.Code, closed.Code], result.Items.Select(x => x.Code));
    }

    [Fact]
    public async Task List_Filters_ReturnOnlyMatchingRecords()
    {
        var projectId = await TestProjects.CreateAsync(database, "12");
        var question = await CreateAsync(projectId, NewIssue("Soru", type: OpenIssueType.Question));
        var late = await CreateAsync(projectId, NewIssue("Gecikmiş", dueDate: Today.AddDays(-2)));
        var closed = await CreateAsync(projectId, NewIssue("Kapalı", dueDate: Today.AddDays(-2)));
        await SetStatusAsync(projectId, closed.Id, OpenIssueStatus.Closed);

        var byType = await ListAsync(projectId, new OpenIssueFilter { Type = OpenIssueType.Question });
        var byStatus = await ListAsync(projectId, new OpenIssueFilter { Status = OpenIssueStatus.Closed });
        var overdue = await ListAsync(projectId, new OpenIssueFilter { OverdueOnly = true });

        Assert.Equal([question.Code], byType.Items.Select(x => x.Code));
        Assert.Equal([closed.Code], byStatus.Items.Select(x => x.Code));
        Assert.Equal([late.Code], overdue.Items.Select(x => x.Code));
    }

    [Fact]
    public async Task List_ProjectMissing_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => ListAsync(int.MaxValue));
    }
}
