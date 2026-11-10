namespace Isdlc.Application.Notifications.Dtos;

/// <summary>The project a notification is about; clicking the notification opens it.</summary>
public record NotificationProjectDto(int Id, string Code, string Name);

/// <summary>One notification of the signed-in user. ReadAt is null while unread.</summary>
public record NotificationDto(
    long Id,
    NotificationProjectDto Project,
    string Message,
    DateTime CreatedAt,
    DateTime? ReadAt);

/// <summary>GET /notifications/unread-count, and how many POST /notifications/read-all marked.</summary>
public record UnreadCountDto(int Count);

/// <summary>?unreadOnly=true</summary>
public record NotificationFilter
{
    public bool UnreadOnly { get; init; }
}
