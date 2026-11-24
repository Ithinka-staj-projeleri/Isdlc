using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Dashboard;
using Isdlc.Application.Dashboard.Dtos;
using Isdlc.Application.Tests.Common;
using Isdlc.Domain.Identity;
using Isdlc.Domain.OpenIssues;
using Isdlc.Domain.Process;
using Isdlc.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Dashboard;

/// <summary>
/// Ekip iş yükü and "Termini bu hafta dolan açık konularım" (Faz 5 · F2) against the contract
/// (docs/dashboard-api.md → "Faz 5: bu hafta ve ekip iş yükü", F2-1…F2-5) on a real database.
/// All tests share one database and other tests leave projects at many steps, so waiting steps are checked as the change a
/// test's own project makes (tests of one collection run one after another). Every test creates its own users and projects.
/// Cross test (PLANNING.md): Baran → Dide's modules.
/// </summary>
[Collection("Database")]
public class TeamWorkloadCrossTests(DatabaseFixture database)
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    // ---- Helpers ----

    private async Task<IReadOnlyList<TeamMemberWorkloadDto>> WorkloadAsync()
    {
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        return await new TeamWorkloadService(db, TimeProvider.System).GetAsync(CancellationToken.None);
    }

    private async Task<TeamMemberWorkloadDto> RowAsync(int userId) => (await WorkloadAsync()).Single(w => w.User.Id == userId);

    private async Task<DashboardDto> DashboardAsync(int userId, DashboardScope scope = DashboardScope.Mine)
    {
        await using var serviceScope = database.CreateScope();
        var db = serviceScope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var service = new DashboardService(
            db, new TestUser(userId), TimeProvider.System, new TeamWorkloadService(db, TimeProvider.System));
        return await service.GetAsync(scope, CancellationToken.None);
    }

    private async Task<int> NewUserAsync(UserRole role, string? name = null)
    {
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var user = new User
        {
            FullName = name ?? $"Çapraz {role}",
            Email = $"workload-{Guid.NewGuid():N}@isdlc.local",
            Role = role,
            CreatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    /// <summary>A project at the step, with the analyst and project manager given (null: nobody assigned).</summary>
    private async Task<int> NewProjectAsync(
        string? stepCode, int? analystId = null, int? managerId = null, ProjectStatus status = ProjectStatus.Active,
        string flowCode = ProcessFlowCodes.NewRequest)
    {
        var projectId = await TestProjects.CreateAsync(database, stepCode, status, flowCode: flowCode);
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var project = await db.Projects.SingleAsync(p => p.Id == projectId);
        project.BusinessAnalystId = analystId;
        project.ProjectManagerId = managerId;
        await db.SaveChangesAsync();
        return projectId;
    }

    private async Task<int> AddIssueAsync(
        int projectId, string code, int? ownerUserId, DateOnly? due = null, OpenIssueStatus status = OpenIssueStatus.Open)
    {
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var issue = new OpenIssue
        {
            ProjectId = projectId, Code = code, Type = OpenIssueType.Action, Title = $"Konu {code}",
            OwnerUserId = ownerUserId, OwnerName = ownerUserId is null ? "Müşteri BT" : null,
            DueDate = due, Status = status, ClosedAt = status == OpenIssueStatus.Closed ? DateTime.UtcNow : null,
        };
        db.OpenIssues.Add(issue);
        await db.SaveChangesAsync();
        return issue.Id;
    }

    /// <summary>A step of the new software project flow that every request reaches (no condition), picked by its roles.</summary>
    private async Task<ProcessStep> StepAsync(Func<ProcessStep, bool> pick)
    {
        await using var scope = database.CreateScope();
        var steps = await scope.ServiceProvider.GetRequiredService<IApplicationDbContext>().ProcessSteps
            .AsNoTracking()
            .Where(s => s.ProcessFlow.Code == ProcessFlowCodes.NewRequest
                && !s.RequiresExternalRequest && s.MinLevel == null)
            .OrderBy(s => s.Order)
            .ToListAsync();
        return steps.First(pick);
    }

    // ---- Waiting steps (F2-3) ----

    [Fact]
    public async Task WaitingSteps_AnalystStep_OnlyTheAssignedAnalyst_UnassignedForEveryAnalyst()
    {
        var step = await StepAsync(s => s.ApproverRole == null && s.ResponsibleRole == UserRole.BusinessAnalyst);
        var mine = await NewUserAsync(UserRole.BusinessAnalyst);
        var other = await NewUserAsync(UserRole.BusinessAnalyst);
        var before = (Mine: (await RowAsync(mine)).WaitingSteps, Other: (await RowAsync(other)).WaitingSteps);

        await NewProjectAsync(step.Code, analystId: mine);      // only mine
        await NewProjectAsync(step.Code, analystId: null);      // nobody assigned: every analyst

        Assert.Equal(before.Mine + 2, (await RowAsync(mine)).WaitingSteps);
        Assert.Equal(before.Other + 1, (await RowAsync(other)).WaitingSteps);
    }

    [Fact]
    public async Task WaitingSteps_ApprovalStep_CountsForTheApproverRoleNotTheResponsible()
    {
        // Step 10: done by Sales, approved by the department manager.
        var step = await StepAsync(s => s.ApproverRole == UserRole.SoftwareDepartmentManager
            && s.ResponsibleRole == UserRole.SalesRepresentative);
        var approver = await NewUserAsync(UserRole.SoftwareDepartmentManager);
        // The seed's Sales user: a new one would change UserServiceTests' "exactly one Sales Representative".
        var responsible = await TestProjects.UserIdAsync(database, TestProjects.Sales);
        var before = (Approver: (await RowAsync(approver)).WaitingSteps, Responsible: (await RowAsync(responsible)).WaitingSteps);

        await NewProjectAsync(step.Code);

        Assert.Equal(before.Approver + 1, (await RowAsync(approver)).WaitingSteps);
        Assert.Equal(before.Responsible, (await RowAsync(responsible)).WaitingSteps);
    }

    // ---- Only active records (F2-2) ----

    [Theory]
    [InlineData(ProjectStatus.Postponed)]
    [InlineData(ProjectStatus.Rejected)]
    public async Task Workload_RecordNotActive_CountsNothing(ProjectStatus status)
    {
        var step = await StepAsync(s => s.ApproverRole == null && s.ResponsibleRole == UserRole.BusinessAnalyst);
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst);
        var before = (await RowAsync(analyst)).WaitingSteps;

        // A postponed request keeps its current step; it still waits for nobody.
        var projectId = await NewProjectAsync(step.Code, analystId: analyst, status: status);
        await AddIssueAsync(projectId, "AK-001", analyst, Today.AddDays(-3));

        var row = await RowAsync(analyst);
        Assert.Equal((0, 0, before), (row.OpenIssues, row.OverdueOpenIssues, row.WaitingSteps));
    }

    [Fact]
    public async Task Workload_EveryFlowCounts_ProcessImprovementToo()
    {
        var developer = await NewUserAsync(UserRole.SoftwareDeveloper);
        var proposalId = await NewProjectAsync(null, flowCode: ProcessFlowCodes.ProcessImprovement);
        await AddIssueAsync(proposalId, "AK-001", developer);

        Assert.Equal(1, (await RowAsync(developer)).OpenIssues);
    }

    [Fact]
    public async Task Workload_OutsideOwner_IsNobodysLoad()
    {
        var developer = await NewUserAsync(UserRole.SoftwareDeveloper);
        var projectId = await NewProjectAsync("12");
        await AddIssueAsync(projectId, "AK-001", developer, Today.AddDays(-1));
        await AddIssueAsync(projectId, "AK-002", ownerUserId: null, Today.AddDays(-1));   // "Müşteri BT", outside the system

        var row = await RowAsync(developer);
        Assert.Equal((1, 1), (row.OpenIssues, row.OverdueOpenIssues));
    }

    [Fact]
    public async Task Workload_DueToday_IsNotOverdue()
    {
        var developer = await NewUserAsync(UserRole.SoftwareDeveloper);
        var projectId = await NewProjectAsync("12");
        await AddIssueAsync(projectId, "AK-001", developer, Today);

        var row = await RowAsync(developer);
        Assert.Equal((1, 0), (row.OpenIssues, row.OverdueOpenIssues));
    }

    // ---- Order (F2-5) and scope (F2-4) ----

    [Fact]
    public async Task Workload_Order_BusiestThenMostOverdueThenTurkishName()
    {
        // Same role, so the same waiting steps: only their open issues tell them apart.
        var tolga = await NewUserAsync(UserRole.SoftwareDeveloper, "Tolga Çapraz");
        var sule = await NewUserAsync(UserRole.SoftwareDeveloper, "Şule Çapraz");     // Ş comes before T in Turkish
        var busy = await NewUserAsync(UserRole.SoftwareDeveloper, "Zeynep Çapraz");
        var late = await NewUserAsync(UserRole.SoftwareDeveloper, "Ahmet Çapraz");
        var projectId = await NewProjectAsync("12");
        await AddIssueAsync(projectId, "AK-001", busy);
        await AddIssueAsync(projectId, "AK-002", busy);
        await AddIssueAsync(projectId, "AK-003", late, Today.AddDays(-1));
        await AddIssueAsync(projectId, "AK-004", sule);
        await AddIssueAsync(projectId, "AK-005", tolga);

        int[] mine = [tolga, sule, busy, late];
        var order = (await WorkloadAsync()).Select(w => w.User.Id).Where(mine.Contains);

        Assert.Equal([busy, late, sule, tolga], order);
    }

    [Fact]
    public async Task Dashboard_TeamWorkload_IsTheSharedServicesRows_ForEveryScope()
    {
        var me = await NewUserAsync(UserRole.SoftwareDeveloper);
        var projectId = await NewProjectAsync("12");
        await AddIssueAsync(projectId, "AK-001", me, Today.AddDays(-2));

        var shared = await WorkloadAsync();
        var mine = (await DashboardAsync(me, DashboardScope.Mine)).TeamWorkload;
        var all = (await DashboardAsync(me, DashboardScope.All)).TeamWorkload;

        // The dashboard, the person profile and the owner suggestion (F9) show the same numbers.
        Assert.Equal(shared, mine);
        Assert.Equal(shared, all);
    }

    // ---- Due this week (F2-1) ----

    [Fact]
    public async Task DueThisWeek_PostponedProject_IsLeftOut_InProgressIncluded()
    {
        var me = await NewUserAsync(UserRole.SoftwareDeveloper);
        var active = await NewProjectAsync("12");
        var postponed = await NewProjectAsync(null, status: ProjectStatus.Postponed);
        var inProgress = await AddIssueAsync(active, "AK-001", me, Today, OpenIssueStatus.InProgress);
        await AddIssueAsync(postponed, "AK-001", me, Today);

        var list = (await DashboardAsync(me)).MyOpenIssuesDueThisWeek;

        Assert.Equal([inProgress], list.Items.Select(x => x.Id));
    }

    [Fact]
    public async Task DueThisWeek_MoreThanTen_ListsTenAndCountsAll()
    {
        var me = await NewUserAsync(UserRole.SoftwareDeveloper);
        var projectId = await NewProjectAsync("12");
        for (var i = 1; i <= 12; i++)
        {
            await AddIssueAsync(projectId, $"AK-{i:000}", me, Today);
        }

        var list = (await DashboardAsync(me)).MyOpenIssuesDueThisWeek;

        Assert.Equal((12, DashboardService.ListSize), (list.TotalCount, list.Items.Count));
    }
}
