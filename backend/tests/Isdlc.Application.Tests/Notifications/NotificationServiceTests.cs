using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Common.Models;
using Isdlc.Application.Notifications;
using Isdlc.Application.Notifications.Dtos;
using Isdlc.Application.Tests.Common;
using Isdlc.Domain.Identity;
using Isdlc.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Isdlc.Application.Tests.Notifications;

/// <summary>
/// Sending notifications and the user's inbox on a real database. Every test sends to its own new project and
/// reads only that project's rows, so other tests' notifications do not matter. Expectations: docs/notifications-api.md.
/// Cross test (PLANNING.md): Erva → Baran's modules.
/// </summary>
[Collection("Database")]
public class NotificationServiceTests(DatabaseFixture database)
{
    private static readonly DateTime SentAt = new(2026, 11, 11, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ReadAt = new(2026, 11, 11, 10, 30, 0, DateTimeKind.Utc);
    private static readonly PageRequest FirstPage = new() { Page = 1, PageSize = 100 };

    private sealed class FixedTime(DateTime utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utc);
    }

    /// <summary>Keeps the errors the service logs.</summary>
    private sealed class RecordingLogger : ILogger<NotificationService>
    {
        public List<string> Errors { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Error)
            {
                Errors.Add(formatter(state, exception));
            }
        }
    }

    private Task<int> UserIdAsync(string email) => TestProjects.UserIdAsync(database, email);

    /// <summary>Sends as the given user (the acting user is never notified), at SentAt.</summary>
    private async Task<RecordingLogger> SendAsync(string? actingEmail, Func<INotificationService, Task> send)
    {
        var actingUserId = actingEmail is null ? (int?)null : await UserIdAsync(actingEmail);
        await using var scope = database.CreateScope();
        var logger = new RecordingLogger();
        var service = new NotificationService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
            new TestUser(actingUserId),
            new FixedTime(SentAt),
            logger);
        await send(service);
        return logger;
    }

    private async Task<T> InboxAsync<T>(string? email, Func<INotificationInboxService, Task<T>> action)
    {
        var userId = email is null ? (int?)null : await UserIdAsync(email);
        await using var scope = database.CreateScope();
        var service = new NotificationInboxService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
            new TestUser(userId),
            new FixedTime(ReadAt));
        return await action(service);
    }

    private async Task<List<Notification>> RowsAsync(int projectId)
    {
        await using var scope = database.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IApplicationDbContext>().Notifications
            .AsNoTracking()
            .Where(n => n.ProjectId == projectId)
            .OrderBy(n => n.Id)
            .ToListAsync();
    }

    /// <summary>The user's notifications about one project, newest first, as the inbox lists them.</summary>
    private async Task<List<NotificationDto>> InboxOfProjectAsync(string email, int projectId, bool unreadOnly = false)
    {
        var page = await InboxAsync(email, s => s.ListAsync(
            new NotificationFilter { UnreadOnly = unreadOnly }, FirstPage, CancellationToken.None));
        return page.Items.Where(n => n.Project.Id == projectId).ToList();
    }

    /// <summary>A user of this test only, so seed users stay as they are.</summary>
    private async Task<int> CreateInactiveUserAsync(UserRole role)
    {
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var user = new User
        {
            FullName = "Pasif Kullanıcı",
            Email = $"pasif-{Guid.NewGuid():N}@isdlc.local",
            Role = role,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    // ---- NotifyUsersAsync ----

    [Fact]
    public async Task NotifyUsers_WritesOneUnreadRowPerUser()
    {
        var projectId = await TestProjects.CreateAsync(database, "4.8");
        var developerId = await UserIdAsync(TestProjects.Developer);
        var projectManagerId = await UserIdAsync(TestProjects.ProjectManager);

        await SendAsync(TestProjects.Analyst, s => s.NotifyUsersAsync(
            [developerId, projectManagerId], projectId, "  RSK-001 size atandı.  ", CancellationToken.None));

        var rows = await RowsAsync(projectId);
        Assert.Equal(new[] { developerId, projectManagerId }.Order(), rows.Select(r => r.UserId).Order());
        Assert.All(rows, row =>
        {
            Assert.Equal("RSK-001 size atandı.", row.Message);
            Assert.Equal(SentAt, row.CreatedAt);
            Assert.Null(row.ReadAt);
        });
    }

    [Fact]
    public async Task NotifyUsers_SkipsActingInactiveUnknownAndRepeatedUsers()
    {
        var projectId = await TestProjects.CreateAsync(database, "4.8");
        var analystId = await UserIdAsync(TestProjects.Analyst);
        var developerId = await UserIdAsync(TestProjects.Developer);
        var inactiveUserId = await CreateInactiveUserAsync(UserRole.SoftwareDeveloper);

        await SendAsync(TestProjects.Analyst, s => s.NotifyUsersAsync(
            [analystId, developerId, developerId, inactiveUserId, int.MaxValue], projectId, "Mesaj",
            CancellationToken.None));

        Assert.Equal([developerId], (await RowsAsync(projectId)).Select(r => r.UserId));
    }

    [Fact]
    public async Task NotifyUsers_OnlyTheActingUser_WritesNothing()
    {
        var projectId = await TestProjects.CreateAsync(database, "4.8");
        var analystId = await UserIdAsync(TestProjects.Analyst);

        await SendAsync(TestProjects.Analyst, s => s.NotifyUsersAsync(
            [analystId], projectId, "Mesaj", CancellationToken.None));
        await SendAsync(TestProjects.Analyst, s => s.NotifyUsersAsync(
            [], projectId, "Mesaj", CancellationToken.None));

        Assert.Empty(await RowsAsync(projectId));
    }

    [Fact]
    public async Task NotifyUsers_LongMessage_IsCutToTheColumnLength()
    {
        var projectId = await TestProjects.CreateAsync(database, "4.8");
        var developerId = await UserIdAsync(TestProjects.Developer);

        await SendAsync(TestProjects.Analyst, s => s.NotifyUsersAsync(
            [developerId], projectId, new string('a', 600), CancellationToken.None));

        var row = Assert.Single(await RowsAsync(projectId));
        Assert.Equal(Notification.MessageMaxLength, row.Message.Length);
        Assert.EndsWith("…", row.Message);
    }

    [Fact]
    public async Task NotifyUsers_SaveFails_LogsAndDoesNotThrowOrBlockTheNextSave()
    {
        // A project that does not exist breaks the foreign key. The module's own work was already saved,
        // so sending must not throw; and the failed rows must not be tried again by the next save.
        var projectId = await TestProjects.CreateAsync(database, "4.8");
        var developerId = await UserIdAsync(TestProjects.Developer);

        var logger = await SendAsync(TestProjects.Analyst, async s =>
        {
            await s.NotifyUsersAsync([developerId], int.MaxValue, "Kaybolur", CancellationToken.None);
            await s.NotifyUsersAsync([developerId], projectId, "Ulaşır", CancellationToken.None);
        });

        Assert.Single(logger.Errors);
        Assert.Equal("Ulaşır", Assert.Single(await RowsAsync(projectId)).Message);
    }

    // ---- NotifyRoleAsync ----

    [Fact]
    public async Task NotifyRole_WritesARowForEveryActiveUserOfTheRole()
    {
        var projectId = await TestProjects.CreateAsync(database, "4.8");
        var inactiveDeveloperId = await CreateInactiveUserAsync(UserRole.SoftwareDeveloper);
        List<int> activeDeveloperIds;
        await using (var scope = database.CreateScope())
        {
            activeDeveloperIds = await scope.ServiceProvider.GetRequiredService<IApplicationDbContext>().Users
                .Where(u => u.Role == UserRole.SoftwareDeveloper && u.IsActive)
                .Select(u => u.Id)
                .ToListAsync();
        }

        await SendAsync(TestProjects.Analyst, s => s.NotifyRoleAsync(
            UserRole.SoftwareDeveloper, projectId, "Geliştirme başladı.", CancellationToken.None));

        var recipients = (await RowsAsync(projectId)).Select(r => r.UserId).ToList();
        Assert.True(activeDeveloperIds.Count >= 2, "Seed has two developers.");
        Assert.Equal(activeDeveloperIds.Order(), recipients.Order());
        Assert.DoesNotContain(inactiveDeveloperId, recipients);
    }

    [Fact]
    public async Task NotifyRole_ActingUserHasTheRole_IsLeftOut()
    {
        var projectId = await TestProjects.CreateAsync(database, "4.8");
        var developerId = await UserIdAsync(TestProjects.Developer);

        await SendAsync(TestProjects.Developer, s => s.NotifyRoleAsync(
            UserRole.SoftwareDeveloper, projectId, "Mesaj", CancellationToken.None));

        var recipients = (await RowsAsync(projectId)).Select(r => r.UserId).ToList();
        Assert.NotEmpty(recipients);
        Assert.DoesNotContain(developerId, recipients);
    }

    // ---- Inbox: list ----

    [Fact]
    public async Task List_ShowsOnlyMyNotificationsNewestFirstWithTheProject()
    {
        var projectId = await TestProjects.CreateAsync(database, "4.8");
        var project = await TestProjects.GetAsync(database, projectId);
        var developerId = await UserIdAsync(TestProjects.Developer);
        var projectManagerId = await UserIdAsync(TestProjects.ProjectManager);
        await SendAsync(TestProjects.Analyst, async s =>
        {
            await s.NotifyUsersAsync([developerId], projectId, "Birinci", CancellationToken.None);
            await s.NotifyUsersAsync([developerId], projectId, "İkinci", CancellationToken.None);
            await s.NotifyUsersAsync([projectManagerId], projectId, "Yöneticiye", CancellationToken.None);
        });

        var mine = await InboxOfProjectAsync(TestProjects.Developer, projectId);

        // Same CreatedAt, so the newer row (higher id) comes first.
        Assert.Equal(["İkinci", "Birinci"], mine.Select(n => n.Message));
        Assert.Equal(new NotificationProjectDto(projectId, project.Code, project.Name), mine[0].Project);
        Assert.Equal(SentAt, mine[0].CreatedAt);
        Assert.Null(mine[0].ReadAt);
    }

    [Fact]
    public async Task List_UnreadOnly_LeavesReadOnesOut()
    {
        var projectId = await TestProjects.CreateAsync(database, "4.8");
        var developerId = await UserIdAsync(TestProjects.Developer);
        await SendAsync(TestProjects.Analyst, async s =>
        {
            await s.NotifyUsersAsync([developerId], projectId, "Okunacak", CancellationToken.None);
            await s.NotifyUsersAsync([developerId], projectId, "Okunmayacak", CancellationToken.None);
        });
        var toRead = (await InboxOfProjectAsync(TestProjects.Developer, projectId)).Single(n => n.Message == "Okunacak");
        await InboxAsync(TestProjects.Developer, s => s.MarkReadAsync(toRead.Id, CancellationToken.None));

        var unread = await InboxOfProjectAsync(TestProjects.Developer, projectId, unreadOnly: true);

        Assert.Equal("Okunmayacak", Assert.Single(unread).Message);
    }

    [Fact]
    public async Task List_NobodySignedIn_ThrowsForbidden()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() => InboxAsync(null,
            s => s.ListAsync(new NotificationFilter(), FirstPage, CancellationToken.None)));
    }

    // ---- Inbox: unread count and marking read ----

    [Fact]
    public async Task UnreadCount_GoesUpWithEachNewNotificationAndDownWhenRead()
    {
        var projectId = await TestProjects.CreateAsync(database, "4.8");
        var developerId = await UserIdAsync(TestProjects.Developer);
        var before = (await InboxAsync(TestProjects.Developer, s => s.GetUnreadCountAsync(CancellationToken.None))).Count;

        await SendAsync(TestProjects.Analyst, async s =>
        {
            await s.NotifyUsersAsync([developerId], projectId, "Bir", CancellationToken.None);
            await s.NotifyUsersAsync([developerId], projectId, "İki", CancellationToken.None);
        });
        var afterSending = (await InboxAsync(TestProjects.Developer, s => s.GetUnreadCountAsync(CancellationToken.None))).Count;

        var first = (await InboxOfProjectAsync(TestProjects.Developer, projectId))[0];
        await InboxAsync(TestProjects.Developer, s => s.MarkReadAsync(first.Id, CancellationToken.None));
        var afterReading = (await InboxAsync(TestProjects.Developer, s => s.GetUnreadCountAsync(CancellationToken.None))).Count;

        Assert.Equal(before + 2, afterSending);
        Assert.Equal(before + 1, afterReading);
    }

    [Fact]
    public async Task MarkRead_SetsReadAtOnceAndKeepsTheFirstTime()
    {
        var projectId = await TestProjects.CreateAsync(database, "4.8");
        var developerId = await UserIdAsync(TestProjects.Developer);
        await SendAsync(TestProjects.Analyst, s => s.NotifyUsersAsync(
            [developerId], projectId, "Mesaj", CancellationToken.None));
        var notification = Assert.Single(await InboxOfProjectAsync(TestProjects.Developer, projectId));

        var read = await InboxAsync(TestProjects.Developer, s => s.MarkReadAsync(notification.Id, CancellationToken.None));

        Assert.Equal(ReadAt, read.ReadAt);

        // Reading again later does not move the time.
        await using var scope = database.CreateScope();
        var again = await new NotificationInboxService(
                scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
                new TestUser(developerId),
                new FixedTime(ReadAt.AddHours(1)))
            .MarkReadAsync(notification.Id, CancellationToken.None);
        Assert.Equal(ReadAt, again.ReadAt);
    }

    [Fact]
    public async Task MarkRead_AnotherUsersNotification_ThrowsNotFoundAndStaysUnread()
    {
        var projectId = await TestProjects.CreateAsync(database, "4.8");
        var developerId = await UserIdAsync(TestProjects.Developer);
        await SendAsync(TestProjects.Analyst, s => s.NotifyUsersAsync(
            [developerId], projectId, "Mesaj", CancellationToken.None));
        var notification = Assert.Single(await InboxOfProjectAsync(TestProjects.Developer, projectId));

        await Assert.ThrowsAsync<NotFoundException>(() => InboxAsync(TestProjects.ProjectManager,
            s => s.MarkReadAsync(notification.Id, CancellationToken.None)));

        Assert.Null(Assert.Single(await RowsAsync(projectId)).ReadAt);
    }

    [Fact]
    public async Task MarkRead_UnknownNotification_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => InboxAsync(TestProjects.Developer,
            s => s.MarkReadAsync(long.MaxValue, CancellationToken.None)));
    }

    [Fact]
    public async Task MarkRead_NobodySignedIn_ThrowsForbidden()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() => InboxAsync(null,
            s => s.MarkReadAsync(1, CancellationToken.None)));
    }

    [Fact]
    public async Task MarkAllRead_MarksOnlyMyUnreadNotifications()
    {
        var projectId = await TestProjects.CreateAsync(database, "4.8");
        var developerId = await UserIdAsync(TestProjects.Developer);
        var projectManagerId = await UserIdAsync(TestProjects.ProjectManager);
        await SendAsync(TestProjects.Analyst, async s =>
        {
            await s.NotifyUsersAsync([developerId], projectId, "Bir", CancellationToken.None);
            await s.NotifyUsersAsync([developerId], projectId, "İki", CancellationToken.None);
            await s.NotifyUsersAsync([projectManagerId], projectId, "Yöneticiye", CancellationToken.None);
        });

        var marked = await InboxAsync(TestProjects.Developer, s => s.MarkAllReadAsync(CancellationToken.None));
        var unreadAfter = await InboxAsync(TestProjects.Developer, s => s.GetUnreadCountAsync(CancellationToken.None));

        Assert.True(marked.Count >= 2);
        Assert.Equal(0, unreadAfter.Count);
        var rows = await RowsAsync(projectId);
        Assert.All(rows.Where(r => r.UserId == developerId), r => Assert.Equal(ReadAt, r.ReadAt));
        Assert.Null(rows.Single(r => r.UserId == projectManagerId).ReadAt);
    }

    [Fact]
    public async Task MarkAllRead_NothingUnread_ReturnsZero()
    {
        await InboxAsync(TestProjects.Developer, s => s.MarkAllReadAsync(CancellationToken.None));

        var marked = await InboxAsync(TestProjects.Developer, s => s.MarkAllReadAsync(CancellationToken.None));

        Assert.Equal(0, marked.Count);
    }
}
