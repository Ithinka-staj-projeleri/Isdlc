using Isdlc.Application.Common.Models;
using Isdlc.Application.Notifications.Dtos;

namespace Isdlc.Application.Notifications;

/// <summary>
/// The signed-in user's own notifications: list, unread count, mark as read. Contract: docs/notifications-api.md
/// Sending is INotificationService's job; this service only reads and marks.
/// </summary>
public interface INotificationInboxService
{
    /// <summary>Newest first.</summary>
    Task<PagedResult<NotificationDto>> ListAsync(
        NotificationFilter filter, PageRequest pageRequest, CancellationToken cancellationToken);

    Task<UnreadCountDto> GetUnreadCountAsync(CancellationToken cancellationToken);

    /// <summary>404 when it does not exist or belongs to someone else. Reading it again keeps the first read time.</summary>
    Task<NotificationDto> MarkReadAsync(long notificationId, CancellationToken cancellationToken);

    /// <summary>Marks every unread notification as read; returns how many were marked.</summary>
    Task<UnreadCountDto> MarkAllReadAsync(CancellationToken cancellationToken);
}
