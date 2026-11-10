using Isdlc.Domain.Notifications;

namespace Isdlc.Application.Notifications;

/// <summary>
/// Who receives a notification and what is stored. No database access, so they can be unit tested directly.
/// Rules: docs/notifications-api.md → "Gönderim kuralları".
/// </summary>
public static class NotificationRules
{
    /// <summary>
    /// The requested users that really get a row: each once, only active ones, never the person who did the action.
    /// The order of the request is kept.
    /// </summary>
    public static IReadOnlyList<int> Recipients(
        IEnumerable<int> requestedUserIds, IReadOnlySet<int> activeUserIds, int? actingUserId) =>
        requestedUserIds
            .Distinct()
            .Where(id => activeUserIds.Contains(id) && id != actingUserId)
            .ToList();

    /// <summary>The message as stored: trimmed, and cut to the column length with "…" when it is longer.</summary>
    public static string Message(string message)
    {
        var text = message.Trim();
        return text.Length <= Notification.MessageMaxLength
            ? text
            : string.Concat(text.AsSpan(0, Notification.MessageMaxLength - 1), "…");
    }
}
