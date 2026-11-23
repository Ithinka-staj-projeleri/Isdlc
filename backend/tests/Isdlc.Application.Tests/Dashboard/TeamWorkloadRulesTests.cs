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

    private static WorkloadStepRow Step(UserRole responsible, UserRole? approver = null, int? analyst = null, int? manager = null) =>
        new(responsible, approver, analyst, manager);

    // ---- Who a waiting step belongs to ----

    [Fact]
    public void IsWaitingFor_ResponsibleRole_WhenThereIsNoApprover()
    {
        Assert.True(TeamWorkloadRules.IsWaitingFor(Step(UserRole.SoftwareDeveloper), 7, UserRole.SoftwareDeveloper));
        Assert.False(TeamWorkloadRules.IsWaitingFor(Step(UserRole.SoftwareDeveloper), 7, UserRole.TestSpecialist));
    }

    [Fact]
    public void IsWaitingFor_ApprovalStep_OnlyTheApprover()
    {
        var step = Step(UserRole.SalesRepresentative, approver: UserRole.SoftwareDepartmentManager);

        Assert.True(TeamWorkloadRules.IsWaitingFor(step, 2, UserRole.SoftwareDepartmentManager));
        Assert.False(TeamWorkloadRules.IsWaitingFor(step, 3, UserRole.SalesRepresentative));
    }

    [Theory]
    [InlineData(null, true)] // nobody assigned: every analyst
    [InlineData(1, true)]    // assigned to them
    [InlineData(9, false)]   // another analyst's record
    public void IsWaitingFor_Analyst_OnlyOnTheirRecordsOrUnassigned(int? assignedAnalyst, bool expected)
    {
        Assert.Equal(expected, TeamWorkloadRules.IsWaitingFor(
            Step(UserRole.BusinessAnalyst, analyst: assignedAnalyst), 1, UserRole.BusinessAnalyst));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(4, true)]
    [InlineData(9, false)]
    public void IsWaitingFor_ProjectManager_OnlyOnTheirRecordsOrUnassigned(int? assignedManager, bool expected)
    {
        Assert.Equal(expected, TeamWorkloadRules.IsWaitingFor(
            Step(UserRole.ProjectManager, manager: assignedManager), 4, UserRole.ProjectManager));
    }

    // ---- The load ----

    [Fact]
    public void Compute_CountsOpenIssuesOverdueAndWaitingSteps_BusiestFirst()
    {
        var users = new[]
        {
            new WorkloadUserRow(1, "Elif Yılmaz", UserRole.BusinessAnalyst),
            new WorkloadUserRow(5, "Can Öztürk", UserRole.SoftwareDeveloper),
            new WorkloadUserRow(6, "Zeynep Arslan", UserRole.SoftwareDeveloper),
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
            Step(UserRole.SoftwareDeveloper),
            Step(UserRole.BusinessAnalyst, analyst: 1),
            Step(UserRole.BusinessAnalyst, analyst: 1),
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
            [new WorkloadUserRow(3, "Burak Şahin", UserRole.TestSpecialist)], [], [], Today);

        var member = Assert.Single(workload);
        Assert.Equal((0, 0, 0), (member.OpenIssues, member.OverdueOpenIssues, member.WaitingSteps));
        Assert.Equal(UserRole.TestSpecialist, member.Role);
    }

    [Fact]
    public void Compute_SameLoad_ByNameInTurkishOrder()
    {
        var workload = TeamWorkloadRules.Compute(
            [
                new WorkloadUserRow(1, "Şule Kaya", UserRole.TestSpecialist),
                new WorkloadUserRow(2, "Selin Koç", UserRole.TestSpecialist),
                new WorkloadUserRow(3, "Ufuk Er", UserRole.TestSpecialist),
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
