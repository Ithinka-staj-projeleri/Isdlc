using System.Linq.Expressions;
using Isdlc.Application.Common;
using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Notifications;
using Isdlc.Application.OpenIssues.Dtos;
using Isdlc.Domain.OpenIssues;
using Microsoft.EntityFrameworkCore;

namespace Isdlc.Application.OpenIssues;

/// <summary>
/// Open issue steps that need the database or notifications, shared by OpenIssueService and MeetingService
/// (meeting actions become open issues and follow the same rules).
/// </summary>
public static class OpenIssueChecks
{
    /// <summary>
    /// Exactly one owner (400), and an owner user must be an active user (400).
    /// messagePrefix tells which row is wrong when several are checked ("2. aksiyon: ").
    /// </summary>
    public static async Task ValidateOwnerAsync(
        this IApplicationDbContext dbContext, int? ownerUserId, string? ownerName, CancellationToken cancellationToken,
        string messagePrefix = "")
    {
        if (OpenIssueRules.ValidateOwner(ownerUserId, ownerName) is { } error)
        {
            throw new BusinessRuleException(messagePrefix + error);
        }

        if (ownerUserId is { } id
            && !await dbContext.Users.AnyAsync(u => u.Id == id && u.IsActive, cancellationToken))
        {
            throw new BusinessRuleException(messagePrefix + "Seçilen sorumlu aktif bir kullanıcı değil.");
        }
    }

    /// <summary>Tells a team member that the record was assigned to them (not when they did it themselves).</summary>
    public static async Task NotifyOwnerAsync(
        this INotificationService notifications, ProjectRef project, OpenIssue issue, int? previousOwnerUserId,
        int actingUserId, CancellationToken cancellationToken)
    {
        if (issue.OwnerUserId is { } ownerId && ownerId != previousOwnerUserId && ownerId != actingUserId)
        {
            await notifications.NotifyUsersAsync(
                [ownerId], project.Id, $"{project.Code} · {issue.Code} size atandı: {issue.Title}", cancellationToken);
        }
    }

    /// <summary>List row of an open issue. today decides IsOverdue.</summary>
    public static Expression<Func<OpenIssue, OpenIssueListItemDto>> ToListItemDto(DateOnly today) =>
        x => new OpenIssueListItemDto(
            x.Id,
            x.Code,
            x.Type,
            x.Title,
            new OwnerDto(x.OwnerUserId, x.OwnerUser != null ? x.OwnerUser.FullName : x.OwnerName!),
            x.DueDate,
            x.Status,
            x.Status != OpenIssueStatus.Closed && x.DueDate != null && x.DueDate < today,
            x.SourceMeeting != null ? new SourceMeetingDto(x.SourceMeeting.Id, x.SourceMeeting.Code) : null);
}
