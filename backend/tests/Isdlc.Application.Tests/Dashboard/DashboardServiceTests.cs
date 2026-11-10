using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Dashboard;
using Isdlc.Application.Dashboard.Dtos;
using Isdlc.Application.Process;
using Isdlc.Application.Tests.Common;
using Isdlc.Domain.Identity;
using Isdlc.Domain.OpenIssues;
using Isdlc.Domain.Process;
using Isdlc.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Dashboard;

/// <summary>
/// The dashboard on a real database. All tests share one database, so "all projects" numbers change as other
/// tests add projects. Each test therefore creates its own users and projects and checks the user's own lists
/// and the "mine" scope, which only they can affect. Expectations: docs/dashboard-api.md
/// </summary>
[Collection("Database")]
public class DashboardServiceTests(DatabaseFixture database)
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private async Task<DashboardDto> AsAsync(int? userId, DashboardScope scope = DashboardScope.Mine)
    {
        await using var scope2 = database.CreateScope();
        var service = new DashboardService(
            scope2.ServiceProvider.GetRequiredService<IApplicationDbContext>(), new TestUser(userId), TimeProvider.System);
        return await service.GetAsync(scope, CancellationToken.None);
    }

    /// <summary>A user of this test only, so seed users and other tests are not affected.</summary>
    private async Task<int> NewUserAsync(UserRole role)
    {
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var user = new User
        {
            FullName = $"Test {role}",
            Email = $"dashboard-{Guid.NewGuid():N}@isdlc.local",
            Role = role,
            CreatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    /// <summary>A new project with the given analyst / project manager (null: nobody assigned).</summary>
    private async Task<int> NewProjectAsync(
        string? stepCode, int? analystId, int? managerId = null,
        ProjectStatus status = ProjectStatus.Active, DateOnly? dueDate = null)
    {
        var projectId = await TestProjects.CreateAsync(database, stepCode, status);
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var project = await db.Projects.SingleAsync(p => p.Id == projectId);
        project.BusinessAnalystId = analystId;
        project.ProjectManagerId = managerId;
        project.DueDate = dueDate;
        await db.SaveChangesAsync();
        return projectId;
    }

    private async Task AddStepRecordAsync(int projectId, string stepCode, DateTime performedAt, int performedById)
    {
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        db.StepRecords.Add(new StepRecord
        {
            ProjectId = projectId,
            ProcessStepId = await TestProjects.StepIdAsync(database, stepCode),
            Action = StepAction.Completed,
            PerformedById = performedById,
            PerformedAt = performedAt,
        });
        await db.SaveChangesAsync();
    }

    private async Task<int> AddOpenIssueAsync(
        int projectId, string code, int ownerUserId, DateOnly? dueDate, OpenIssueStatus status = OpenIssueStatus.Open)
    {
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var issue = new OpenIssue
        {
            ProjectId = projectId,
            Code = code,
            Type = OpenIssueType.Action,
            Title = $"Konu {code}",
            OwnerUserId = ownerUserId,
            DueDate = dueDate,
            Status = status,
            ClosedAt = status == OpenIssueStatus.Closed ? DateTime.UtcNow : null,
        };
        db.OpenIssues.Add(issue);
        await db.SaveChangesAsync();
        return issue.Id;
    }

    // ---- Who is signed in ----

    [Fact]
    public async Task Get_NotSignedIn_ThrowsForbidden()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() => AsAsync(null));
    }

    // ---- My waiting steps (decisions 2 and 3) ----

    [Fact]
    public async Task MyWaitingSteps_AnalystStep_OnlyTheAssignedAnalystSeesIt()
    {
        var mine = await NewUserAsync(UserRole.BusinessAnalyst);
        var other = await NewUserAsync(UserRole.BusinessAnalyst);
        var projectId = await NewProjectAsync("4.1", analystId: mine);

        await WaitingSinceLongAgoAsync(projectId, "3.2");

        var forMine = await AsAsync(mine);
        var forOther = await AsAsync(other);

        // Unassigned projects of other tests are shown to every analyst too (decision 3), so look for this one.
        var item = Assert.Single(forMine.MyWaitingSteps.Items, x => x.Project.Id == projectId);
        Assert.Equal("4.1", item.Step.Code);
        Assert.Equal(4, item.Step.StageOrder);
        Assert.False(item.IsApproval);
        Assert.DoesNotContain(forOther.MyWaitingSteps.Items, x => x.Project.Id == projectId);
    }

    [Fact]
    public async Task MyWaitingSteps_NoAnalystAssigned_EveryAnalystSeesIt()
    {
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst);
        var projectId = await NewProjectAsync("7.1", analystId: null);
        await WaitingSinceLongAgoAsync(projectId, "7.1");

        Assert.True(await IsWaitingForAsync(analyst, projectId));
    }

    [Fact]
    public async Task MyWaitingSteps_ProjectManagerStep_OnlyTheAssignedManagerSeesIt()
    {
        var mine = await NewUserAsync(UserRole.ProjectManager);
        var other = await NewUserAsync(UserRole.ProjectManager);
        var projectId = await NewProjectAsync("10.1", analystId: null, managerId: mine);
        await WaitingSinceLongAgoAsync(projectId, "10.1");

        Assert.True(await IsWaitingForAsync(mine, projectId));
        Assert.False(await IsWaitingForAsync(other, projectId));
    }

    [Fact]
    public async Task MyWaitingSteps_ApprovalStep_ShownToTheApproverRoleNotTheResponsibleRole()
    {
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst);
        var manager = await NewUserAsync(UserRole.SoftwareDepartmentManager);
        var projectId = await NewProjectAsync("4.5", analystId: analyst);
        await WaitingSinceLongAgoAsync(projectId, "4.4");

        // 4.5 is the analyst's step, but its approver is the department manager: only the manager can act on it.
        Assert.False(await IsWaitingForAsync(analyst, projectId));
        var item = await WaitingItemAsync(manager, projectId);
        Assert.True(item.IsApproval);
        Assert.True(item.IsDecision);
    }

    [Fact]
    public async Task MyWaitingSteps_ProjectNotActive_IsNotShown()
    {
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst);
        var projectId = await NewProjectAsync(null, analystId: analyst, status: ProjectStatus.Lost);

        var dashboard = await AsAsync(analyst);

        Assert.DoesNotContain(dashboard.MyWaitingSteps.Items, x => x.Project.Id == projectId);
        Assert.Equal(dashboard.MyWaitingSteps.TotalCount, dashboard.Summary.MyWaitingSteps);
    }

    [Fact]
    public async Task MyWaitingSteps_LongestWaitingFirst_FromTheLastProcessRecord()
    {
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst);
        var newer = await NewProjectAsync("4.1", analystId: analyst);
        var older = await NewProjectAsync("7.1", analystId: analyst);
        await AddStepRecordAsync(newer, "4.1", new DateTime(2000, 1, 3, 9, 0, 0, DateTimeKind.Utc), analyst);
        await AddStepRecordAsync(older, "7.1", new DateTime(2000, 1, 1, 9, 0, 0, DateTimeKind.Utc), analyst);

        var dashboard = await AsAsync(analyst);

        // Only this test's projects; unassigned projects of other tests may be listed too (decision 3).
        var mine = dashboard.MyWaitingSteps.Items.Where(x => x.Project.Id == newer || x.Project.Id == older).ToList();
        Assert.Equal([older, newer], mine.Select(x => x.Project.Id));
        Assert.Equal(2, mine[0].WaitingDays - mine[1].WaitingDays);
        Assert.Equal(new DateTime(2000, 1, 1, 9, 0, 0, DateTimeKind.Utc), mine[0].WaitingSince);
    }

    [Fact]
    public async Task MyWaitingSteps_EveryItem_IsAllowedByTheProcessRule()
    {
        var manager = await NewUserAsync(UserRole.SoftwareDepartmentManager);
        await NewProjectAsync("4.5", analystId: null);
        await NewProjectAsync("5.1", analystId: null);

        var dashboard = await AsAsync(manager, DashboardScope.All);

        // The dashboard query and ProcessProgress.CanActOnStep must agree on every row.
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var stepIds = dashboard.MyWaitingSteps.Items.Select(x => x.Step.Id).ToList();
        var steps = await db.ProcessSteps.Where(s => stepIds.Contains(s.Id)).ToListAsync();
        Assert.NotEmpty(steps);
        Assert.All(steps, s => Assert.True(ProcessProgress.CanActOnStep(
            new StepPermissionInfo(s.IsDecision, s.ResponsibleRole, s.ApproverRole), UserRole.SoftwareDepartmentManager)));
    }

    // ---- Summary and stages ("mine" scope) ----

    [Fact]
    public async Task Summary_Mine_CountsOnlyTheUsersProjects()
    {
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst);
        var manager = await NewUserAsync(UserRole.ProjectManager);
        await NewProjectAsync("1.1", analystId: analyst);                                   // request, stage 1
        await NewProjectAsync("2.1", analystId: analyst);                                   // request, stage 2
        await NewProjectAsync("4.1", analystId: null, managerId: analyst);                  // project, stage 4 (the user as its manager)
        await NewProjectAsync("7.1", analystId: analyst, dueDate: Today.AddDays(-1));       // project, stage 7, late
        await NewProjectAsync(null, analystId: analyst, status: ProjectStatus.Closed);
        await NewProjectAsync(null, analystId: analyst, status: ProjectStatus.Lost);
        await NewProjectAsync("4.1", analystId: null, managerId: manager);                  // not the analyst's

        var dashboard = await AsAsync(analyst);

        Assert.Equal(DashboardScope.Mine, dashboard.Scope);
        Assert.Equal(2, dashboard.Summary.WaitingRequests);
        Assert.Equal(2, dashboard.Summary.ActiveProjects);
        Assert.Equal(1, dashboard.Summary.OverdueProjects);
        Assert.Equal(new FinishedThisYearDto(1, 1, 0, 0), dashboard.Summary.FinishedThisYear);
        Assert.Equal(13, dashboard.Stages.Count);
        Assert.Equal(Enumerable.Range(1, 13), dashboard.Stages.Select(s => s.StageOrder));
        Assert.Equal([1, 1, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0], dashboard.Stages.Select(s => s.Count));
    }

    [Fact]
    public async Task Summary_FinishedLastYear_IsNotCounted()
    {
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst);
        var projectId = await NewProjectAsync(null, analystId: analyst, status: ProjectStatus.Rejected);
        await AddStepRecordAsync(projectId, "3.2", new DateTime(DateTime.UtcNow.Year - 1, 6, 1, 9, 0, 0, DateTimeKind.Utc), analyst);

        var dashboard = await AsAsync(analyst);

        Assert.Equal(new FinishedThisYearDto(0, 0, 0, 0), dashboard.Summary.FinishedThisYear);
    }

    // ---- Open issues ----

    [Fact]
    public async Task MyOpenIssues_NotClosedOnActiveProjects_OverdueFirst()
    {
        var owner = await NewUserAsync(UserRole.SoftwareDeveloper);
        var projectId = await NewProjectAsync("7.1", analystId: null);
        var closedProjectId = await NewProjectAsync(null, analystId: null, status: ProjectStatus.Closed);
        var noDue = await AddOpenIssueAsync(projectId, "AK-001", owner, null);
        var later = await AddOpenIssueAsync(projectId, "AK-002", owner, Today.AddDays(5));
        var late = await AddOpenIssueAsync(projectId, "AK-003", owner, Today.AddDays(-1));
        await AddOpenIssueAsync(projectId, "AK-004", owner, Today.AddDays(-3), OpenIssueStatus.Closed);
        await AddOpenIssueAsync(closedProjectId, "AK-001", owner, Today.AddDays(-3));

        var dashboard = await AsAsync(owner);

        Assert.Equal(3, dashboard.MyOpenIssues.TotalCount);
        Assert.Equal([late, later, noDue], dashboard.MyOpenIssues.Items.Select(x => x.Id));
        Assert.True(dashboard.MyOpenIssues.Items[0].IsOverdue);
        Assert.Equal("Test SoftwareDeveloper", dashboard.MyOpenIssues.Items[0].Owner.Name);
    }

    [Fact]
    public async Task OverdueOpenIssues_Mine_OnlyActiveProjectsAndNotClosed()
    {
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst);
        var owner = await NewUserAsync(UserRole.SoftwareDeveloper);
        var projectId = await NewProjectAsync("7.1", analystId: analyst);
        var closedProjectId = await NewProjectAsync(null, analystId: analyst, status: ProjectStatus.Closed);
        var oldest = await AddOpenIssueAsync(projectId, "AK-001", owner, Today.AddDays(-10));
        var late = await AddOpenIssueAsync(projectId, "AK-002", owner, Today.AddDays(-1));
        await AddOpenIssueAsync(projectId, "AK-003", owner, Today);                                 // due today: not late
        await AddOpenIssueAsync(projectId, "AK-004", owner, Today.AddDays(-5), OpenIssueStatus.Closed);
        await AddOpenIssueAsync(closedProjectId, "AK-001", owner, Today.AddDays(-5));

        var dashboard = await AsAsync(analyst);

        Assert.Equal(2, dashboard.Summary.OverdueOpenIssues);
        Assert.Equal([oldest, late], dashboard.OverdueOpenIssues.Items.Select(x => x.Id));
    }

    // ---- Recent activity ----

    [Fact]
    public async Task RecentActivity_Mine_NewestFirstFromTheUsersProjects()
    {
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst);
        var projectId = await NewProjectAsync("2.1", analystId: analyst);
        var otherProjectId = await NewProjectAsync("2.1", analystId: null);
        await AddStepRecordAsync(projectId, "1.1", DateTime.UtcNow.AddHours(-3), analyst);
        await AddStepRecordAsync(projectId, "1.2", DateTime.UtcNow.AddHours(-1), analyst);
        await AddStepRecordAsync(otherProjectId, "1.1", DateTime.UtcNow, analyst);

        var dashboard = await AsAsync(analyst);

        Assert.Equal(["1.2", "1.1"], dashboard.RecentActivity.Select(x => x.StepCode));
        Assert.All(dashboard.RecentActivity, x => Assert.Equal(projectId, x.Project.Id));
        Assert.Equal("Test BusinessAnalyst", dashboard.RecentActivity[0].PerformedBy.FullName);
    }

    // ---- Helpers ----

    /// <summary>
    /// The list shows the 10 longest waiting rows, and other tests leave projects at the same steps.
    /// A very old process record puts this project first, so the check never depends on them.
    /// </summary>
    private async Task WaitingSinceLongAgoAsync(int projectId, string stepCode)
    {
        var someone = await TestProjects.UserIdAsync(database, TestProjects.Analyst);
        await AddStepRecordAsync(projectId, stepCode, new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc), someone);
    }

    private async Task<bool> IsWaitingForAsync(int userId, int projectId)
    {
        var dashboard = await AsAsync(userId, DashboardScope.All);
        return dashboard.MyWaitingSteps.Items.Any(x => x.Project.Id == projectId);
    }

    private async Task<WaitingStepDto> WaitingItemAsync(int userId, int projectId)
    {
        var dashboard = await AsAsync(userId, DashboardScope.All);
        return Assert.Single(dashboard.MyWaitingSteps.Items, x => x.Project.Id == projectId);
    }
}
