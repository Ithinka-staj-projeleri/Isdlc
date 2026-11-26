using Isdlc.Application.Dashboard;
using Isdlc.Application.Dashboard.Dtos;
using Isdlc.Domain.Identity;
using Isdlc.Domain.OpenIssues;

namespace Isdlc.Application.Tests.Dashboard;

/// <summary>
/// Ekip iş yükü and the "this week" dates (Faz 5 · F2), no database. Expectations: docs/dashboard-api.md → "Ekip iş yükü".
/// Cross test (PLANNING.md): Baran → Dide's modules.
/// </summary>
public class TeamWorkloadRulesTests
{
    private static readonly DateOnly Today = new(2026, 11, 25); // a Wednesday

    private static WorkloadStepRow Step(Permission responsible, Permission? approver = null, int? analyst = null, int? manager = null) =>
        new(responsible, approver, analyst, manager);

    // ---- Who a waiting step belongs to ----

    [Fact]
    public void IsWaitingFor_ResponsiblePermission_WhenThereIsNoApprover()
    {
        Assert.True(TeamWorkloadRules.IsWaitingFor(Step(Permission.Development), 7, [Permission.Development]));
        Assert.False(TeamWorkloadRules.IsWaitingFor(Step(Permission.Development), 7, [Permission.Testing]));
    }

    [Fact]
    public void IsWaitingFor_ApprovalStep_OnlyTheApprover()
    {
        var step = Step(Permission.Sales, approver: Permission.Approval);

        Assert.True(TeamWorkloadRules.IsWaitingFor(step, 2, [Permission.Approval]));
        Assert.False(TeamWorkloadRules.IsWaitingFor(step, 3, [Permission.Sales]));
    }

    [Theory]
    [InlineData(null, true)] // nobody assigned: every analyst
    [InlineData(1, true)]    // assigned to them
    [InlineData(9, false)]   // another analyst's record
    public void IsWaitingFor_Analyst_OnlyOnTheirRecordsOrUnassigned(int? assignedAnalyst, bool expected)
    {
        Assert.Equal(expected, TeamWorkloadRules.IsWaitingFor(
            Step(Permission.Analysis, analyst: assignedAnalyst), 1, [Permission.Analysis]));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(4, true)]
    [InlineData(9, false)]
    public void IsWaitingFor_ProjectManager_OnlyOnTheirRecordsOrUnassigned(int? assignedManager, bool expected)
    {
        Assert.Equal(expected, TeamWorkloadRules.IsWaitingFor(
            Step(Permission.ProjectManagement, manager: assignedManager), 4, [Permission.ProjectManagement]));
    }

    [Fact]
    public void IsWaitingFor_SeveralPermissions_AnyOfThem()
    {
        var both = new[] { Permission.Development, Permission.Testing };

        Assert.True(TeamWorkloadRules.IsWaitingFor(Step(Permission.Testing), 7, both));
        Assert.True(TeamWorkloadRules.IsWaitingFor(Step(Permission.Development), 7, both));
        Assert.False(TeamWorkloadRules.IsWaitingFor(Step(Permission.Sales), 7, both));
        Assert.False(TeamWorkloadRules.IsWaitingFor(Step(Permission.Development), 7, []));
    }

    // ---- The load ----

    [Fact]
    public void Compute_CountsOpenIssuesOverdueAndWaitingSteps_BusiestFirst()
    {
        var users = new[]
        {
            new WorkloadUserRow(1, "Elif Yılmaz", [Permission.Analysis]),
            new WorkloadUserRow(5, "Can Öztürk", [Permission.Development]),
            new WorkloadUserRow(6, "Zeynep Arslan", [Permission.Development]),
        };
        var issues = new[]
        {
            new WorkloadOpenIssueRow(5, OpenIssueStatus.Open, Today.AddDays(-1)),       // overdue
            new WorkloadOpenIssueRow(5, OpenIssueStatus.InProgress, Today),
            new WorkloadOpenIssueRow(5, OpenIssueStatus.Closed, Today.AddDays(-9)),     // closed: not counted
            new WorkloadOpenIssueRow(1, OpenIssueStatus.Open, null),
        };
        var steps = new[]
        {
            Step(Permission.Development),
            Step(Permission.Analysis, analyst: 1),
            Step(Permission.Analysis, analyst: 1),
        };

        var workload = TeamWorkloadRules.Compute(users, issues, steps, Today);

        Assert.Equal(
            [
                (5, 2, 1, 1), // Can: 2 open (1 overdue), 1 developer step → 3
                (1, 1, 0, 2), // Elif: 1 open, 2 steps → 3, no overdue
                (6, 0, 0, 1), // Zeynep: the developer step is waiting for every developer
            ],
            workload.Select(w => (w.User.Id, w.OpenIssues, w.OverdueOpenIssues, w.WaitingSteps)));
    }

    [Fact]
    public void Compute_EveryUserIsListed_EvenWithNothing()
    {
        var workload = TeamWorkloadRules.Compute(
            [new WorkloadUserRow(3, "Burak Şahin", [Permission.Testing])], [], [], Today);

        var member = Assert.Single(workload);
        Assert.Equal((0, 0, 0), (member.OpenIssues, member.OverdueOpenIssues, member.WaitingSteps));
        Assert.Equal([Permission.Testing], member.Permissions);
    }

    [Fact]
    public void Compute_SameLoad_ByNameInTurkishOrder()
    {
        var workload = TeamWorkloadRules.Compute(
            [
                new WorkloadUserRow(1, "Şule Kaya", [Permission.Testing]),
                new WorkloadUserRow(2, "Selin Koç", [Permission.Testing]),
                new WorkloadUserRow(3, "Ufuk Er", [Permission.Testing]),
            ],
            [], [], Today);

        Assert.Equal(["Selin Koç", "Şule Kaya", "Ufuk Er"], workload.Select(w => w.User.FullName));
    }

    // ---- This week ----

    [Theory]
    [InlineData("2026-11-23", "2026-11-29")] // Monday
    [InlineData("2026-11-25", "2026-11-29")] // Wednesday
    [InlineData("2026-11-29", "2026-11-29")] // Sunday: its own week's end
    public void EndOfWeek_IsTheSundayOfTheWeek(string day, string expected)
    {
        Assert.Equal(DateOnly.Parse(expected), TeamWorkloadRules.EndOfWeek(DateOnly.Parse(day)));
    }
}
