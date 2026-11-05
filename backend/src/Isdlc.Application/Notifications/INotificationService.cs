using Isdlc.Domain.Identity;

namespace Isdlc.Application.Notifications;

/// <summary>
/// In-app notifications. Other modules send notifications only through this interface.
/// Until the Notifications module is built, NullNotificationService is registered.
/// </summary>
public interface INotificationService
{
    /// <summary>Notifies every active user with the role.</summary>
    Task NotifyRoleAsync(UserRole role, int projectId, string message, CancellationToken cancellationToken);

    Task NotifyUsersAsync(IEnumerable<int> userIds, int projectId, string message, CancellationToken cancellationToken);
}
