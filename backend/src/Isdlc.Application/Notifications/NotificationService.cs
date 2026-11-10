using Isdlc.Application.Common.Interfaces;
using Isdlc.Domain.Identity;
using Isdlc.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Isdlc.Application.Notifications;

/// <summary>
/// Writes in-app notifications. Modules call it after their own save succeeded, so a failure here must not undo or
/// fail their work: it is logged and the notification is lost (docs/notifications-api.md → "Gönderim kuralları").
/// </summary>
public class NotificationService(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUser,
    TimeProvider timeProvider,
    ILogger<NotificationService> logger) : INotificationService
{
    public async Task NotifyRoleAsync(UserRole role, int projectId, string message, CancellationToken cancellationToken)
    {
        // One row per user who has the role now; whoever gets the role later does not see old notifications.
        await SendAsync(
            () => dbContext.Users.AsNoTracking().Where(u => u.Role == role).Select(u => u.Id).ToListAsync(cancellationToken),
            projectId, message, cancellationToken);
    }

    public async Task NotifyUsersAsync(
        IEnumerable<int> userIds, int projectId, string message, CancellationToken cancellationToken)
    {
        var requested = userIds.ToList();
        await SendAsync(() => Task.FromResult(requested), projectId, message, cancellationToken);
    }

    private async Task SendAsync(
        Func<Task<List<int>>> requestedUserIds, int projectId, string message, CancellationToken cancellationToken)
    {
        var added = new List<Notification>();
        try
        {
            var requested = await requestedUserIds();
            if (requested.Count == 0)
            {
                return;
            }

            var active = await dbContext.Users
                .AsNoTracking()
                .Where(u => requested.Contains(u.Id) && u.IsActive)
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);

            var recipients = NotificationRules.Recipients(requested, active.ToHashSet(), currentUser.UserId);
            if (recipients.Count == 0)
            {
                return;
            }

            var now = timeProvider.GetUtcNow().UtcDateTime;
            var text = NotificationRules.Message(message);
            added.AddRange(recipients.Select(userId => new Notification
            {
                UserId = userId,
                ProjectId = projectId,
                Message = text,
                CreatedAt = now,
            }));
            dbContext.Notifications.AddRange(added);

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The rows stay out of the shared DbContext, so the module's next save does not try them again.
            foreach (var notification in added)
            {
                dbContext.Entry(notification).State = EntityState.Detached;
            }

            logger.LogError(ex, "Notification could not be saved. Project {ProjectId}: {Message}", projectId, message);
        }
    }
}
