using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Notifications;
using Isdlc.Domain.Identity;

namespace Isdlc.Application.Tests.Common;

/// <summary>"This user is signed in" for one service under test. Null means nobody is signed in.</summary>
public sealed class TestUser(int? userId) : ICurrentUserService
{
    public int? UserId { get; } = userId;
}

/// <summary>Keeps every notification instead of sending it, so a test can check what was sent.</summary>
public sealed class RecordingNotificationService : INotificationService
{
    public List<SentNotification> Sent { get; } = [];

    public Task NotifyRoleAsync(UserRole role, int projectId, string message, CancellationToken cancellationToken)
    {
        Sent.Add(new SentNotification(role, [], projectId, message));
        return Task.CompletedTask;
    }

    public Task NotifyUsersAsync(IEnumerable<int> userIds, int projectId, string message, CancellationToken cancellationToken)
    {
        Sent.Add(new SentNotification(null, userIds.ToList(), projectId, message));
        return Task.CompletedTask;
    }
}

/// <summary>One notification: to a role, or to a list of users.</summary>
public sealed record SentNotification(UserRole? Role, IReadOnlyList<int> UserIds, int ProjectId, string Message);
