using System.Globalization;
using Isdlc.Application.Common.Models;
using Isdlc.Application.Dashboard.Dtos;
using Isdlc.Application.OpenIssues;
using Isdlc.Domain.Identity;
using Isdlc.Domain.OpenIssues;

namespace Isdlc.Application.Dashboard;

/// <summary>
/// Ekip iş yükü and the dates of the dashboard's "this week" list (Faz 5 · F2). Pure rules with no database access.
/// Contract: docs/dashboard-api.md → "Ekip iş yükü".
/// </summary>
public static class TeamWorkloadRules
{
    private static readonly StringComparer TurkishNames = StringComparer.Create(new CultureInfo("tr-TR"), ignoreCase: true);

    /// <summary>
    /// Who a waiting step belongs to — the same rule as "Bana bekleyen adımlar": the user has the step's approver
    /// permission, or its responsible permission when there is no approver; an analysis or project management step only
    /// on records the user is assigned to, or nobody is.
    /// </summary>
    public static bool IsWaitingFor(WorkloadStepRow step, int userId, IReadOnlyCollection<Permission> permissions)
    {
        var needed = step.ApproverPermission ?? step.ResponsiblePermission;
        if (!permissions.Contains(needed))
        {
            return false;
        }

        return needed switch
        {
            Permission.Analysis => step.BusinessAnalystId is null || step.BusinessAnalystId == userId,
            Permission.ProjectManagement => step.ProjectManagerId is null || step.ProjectManagerId == userId,
            _ => true,
        };
    }

    /// <summary>Every user with their load, the busiest first (open issues + waiting steps), then by name.</summary>
    public static IReadOnlyList<TeamMemberWorkloadDto> Compute(
        IReadOnlyCollection<WorkloadUserRow> users,
        IReadOnlyCollection<WorkloadOpenIssueRow> openIssues,
        IReadOnlyCollection<WorkloadStepRow> steps,
        DateOnly today) =>
        users
            .Select(user =>
            {
                var owned = openIssues.Where(i => i.OwnerUserId == user.Id && i.Status != OpenIssueStatus.Closed).ToList();
                return new TeamMemberWorkloadDto(
                    new UserSummaryDto(user.Id, user.FullName),
                    user.Permissions,
                    owned.Count,
                    owned.Count(i => OpenIssueRules.IsOverdue(i.Status, i.DueDate, today)),
                    steps.Count(s => IsWaitingFor(s, user.Id, user.Permissions)));
            })
            .OrderByDescending(w => w.OpenIssues + w.WaitingSteps)
            .ThenByDescending(w => w.OverdueOpenIssues)
            .ThenBy(w => w.User.FullName, TurkishNames)
            .ToList();

    /// <summary>The Sunday that ends the week of the day (weeks run Monday to Sunday); a Sunday is its own week's end.</summary>
    public static DateOnly EndOfWeek(DateOnly day) => day.AddDays((7 - (int)day.DayOfWeek) % 7);
}
