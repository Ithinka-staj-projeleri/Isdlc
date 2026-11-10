using Isdlc.Api.Common;
using Isdlc.Application.Common.Models;
using Isdlc.Application.Notifications;
using Isdlc.Application.Notifications.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Isdlc.Api.Controllers;

/// <summary>
/// The signed-in user's own notifications: /api/v1/notifications. Contract: docs/notifications-api.md
/// Modules do not call this; they send through INotificationService.
/// </summary>
public class NotificationsController(INotificationInboxService inboxService) : ApiControllerBase
{
    /// <summary>Newest first. Filter: ?unreadOnly=true. Paging: ?page=1&amp;pageSize=20</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<NotificationDto>>(StatusCodes.Status200OK)]
    public async Task<PagedResult<NotificationDto>> List(
        [FromQuery] NotificationFilter filter, [FromQuery] PageRequest pageRequest, CancellationToken cancellationToken)
    {
        return await inboxService.ListAsync(filter, pageRequest, cancellationToken);
    }

    /// <summary>The number on the bell.</summary>
    [HttpGet("unread-count")]
    [ProducesResponseType<UnreadCountDto>(StatusCodes.Status200OK)]
    public async Task<UnreadCountDto> UnreadCount(CancellationToken cancellationToken)
    {
        return await inboxService.GetUnreadCountAsync(cancellationToken);
    }

    [HttpPost("{notificationId:long}/read")]
    [ProducesResponseType<NotificationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<NotificationDto> MarkRead(long notificationId, CancellationToken cancellationToken)
    {
        return await inboxService.MarkReadAsync(notificationId, cancellationToken);
    }

    /// <summary>Marks every unread notification as read; returns how many were marked.</summary>
    [HttpPost("read-all")]
    [ProducesResponseType<UnreadCountDto>(StatusCodes.Status200OK)]
    public async Task<UnreadCountDto> MarkAllRead(CancellationToken cancellationToken)
    {
        return await inboxService.MarkAllReadAsync(cancellationToken);
    }
}
