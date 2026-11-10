using System.Linq.Expressions;
using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Common.Models;
using Isdlc.Application.Notifications.Dtos;
using Isdlc.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Isdlc.Application.Notifications;

public class NotificationInboxService(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUser,
    TimeProvider timeProvider) : INotificationInboxService
{
    public async Task<PagedResult<NotificationDto>> ListAsync(
        NotificationFilter filter, PageRequest pageRequest, CancellationToken cancellationToken)
    {
        var query = Mine();
        if (filter.UnreadOnly)
        {
            query = query.Where(n => n.ReadAt == null);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .ApplyPaging(pageRequest)
            .Select(ToDto)
            .ToListAsync(cancellationToken);

        return new PagedResult<NotificationDto>(items, totalCount);
    }

    public async Task<UnreadCountDto> GetUnreadCountAsync(CancellationToken cancellationToken) =>
        new(await Mine().CountAsync(n => n.ReadAt == null, cancellationToken));

    public async Task<NotificationDto> MarkReadAsync(long notificationId, CancellationToken cancellationToken)
    {
        var userId = RequireSignedIn();

        // Another user's notification is answered as if it did not exist.
        var notification = await dbContext.Notifications
            .SingleOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Bildirim bulunamadı.");

        if (notification.ReadAt is null)
        {
            notification.ReadAt = timeProvider.GetUtcNow().UtcDateTime;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return await Mine().Where(n => n.Id == notificationId).Select(ToDto).SingleAsync(cancellationToken);
    }

    public async Task<UnreadCountDto> MarkAllReadAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // One UPDATE in the database; notifications are not audited, so no interceptor needs to see them.
        var marked = await Mine()
            .Where(n => n.ReadAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(n => n.ReadAt, now), cancellationToken);

        return new UnreadCountDto(marked);
    }

    // ---- Helpers ----

    private int RequireSignedIn() =>
        currentUser.UserId ?? throw new ForbiddenException("İşlem yapmak için giriş yapılmalıdır.");

    /// <summary>Only the signed-in user's notifications.</summary>
    private IQueryable<Notification> Mine()
    {
        var userId = RequireSignedIn();
        return dbContext.Notifications.AsNoTracking().Where(n => n.UserId == userId);
    }

    private static readonly Expression<Func<Notification, NotificationDto>> ToDto = n => new NotificationDto(
        n.Id,
        new NotificationProjectDto(n.Project.Id, n.Project.Code, n.Project.Name),
        n.Message,
        n.CreatedAt,
        n.ReadAt);
}
