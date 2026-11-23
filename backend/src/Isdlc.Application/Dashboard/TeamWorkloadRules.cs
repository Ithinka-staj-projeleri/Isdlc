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
    /// Who a waiting step belongs to — the same rule as "Bana bekleyen adımlar": the approver role, or the responsible role
    /// when there is no approver; an analyst or project manager only on records they are assigned to, or nobody is.
    /// </summary>
    public static bool IsWaitingFor(WorkloadStepRow step, int userId, UserRole role)
    {
        if (role != (step.ApproverRole ?? step.ResponsibleRole))
        {
            return false;
        }

        return role switch
        {
            UserRole.BusinessAnalyst => step.BusinessAnalystId is null || step.BusinessAnalystId == userId,
            UserRole.ProjectManager => step.ProjectManagerId is null || step.ProjectManagerId == userId,
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
                    user.Role,
                    owned.Count,
                    owned.Count(i => OpenIssueRules.IsOverdue(i.Status, i.DueDate, today)),
                    steps.Count(s => IsWaitingFor(s, user.Id, user.Role)));
            })
            .OrderByDescending(w => w.OpenIssues + w.WaitingSteps)
            .ThenByDescending(w => w.OverdueOpenIssues)
            .ThenBy(w => w.User.FullName, TurkishNames)
            .ToList();

    /// <summary>The Sunday that ends the week of the day (weeks run Monday to Sunday); a Sunday is its own week's end.</summary>
    public static DateOnly EndOfWeek(DateOnly day) => day.AddDays((7 - (int)day.DayOfWeek) % 7);
}
