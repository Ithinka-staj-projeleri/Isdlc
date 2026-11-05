using Isdlc.Domain.Identity;
using Microsoft.Extensions.Logging;

namespace Isdlc.Application.Notifications;

/// <summary>
/// TEMPORARY: sends nothing, only writes the notification to the log.
/// The Notifications module replaces its registration in DependencyInjection.cs.
/// </summary>
public class NullNotificationService(ILogger<NullNotificationService> logger) : INotificationService
{
    public Task NotifyRoleAsync(UserRole role, int projectId, string message, CancellationToken cancellationToken)
    {
        logger.LogInformation("Notification not sent (no Notifications module yet). Role {Role}, project {ProjectId}: {Message}",
            role, projectId, message);
        return Task.CompletedTask;
    }

    public Task NotifyUsersAsync(IEnumerable<int> userIds, int projectId, string message, CancellationToken cancellationToken)
    {
        logger.LogInformation("Notification not sent (no Notifications module yet). Users {UserIds}, project {ProjectId}: {Message}",
            string.Join(",", userIds), projectId, message);
        return Task.CompletedTask;
    }
}
