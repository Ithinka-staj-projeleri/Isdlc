using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Identity;
using Isdlc.Application.Notifications;
using Isdlc.Application.Tests.Common;
using Isdlc.Domain.Identity;
using Isdlc.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Isdlc.Application.Tests.Tenants;

/// <summary>
/// People and notifications across companies (Faz 6 · T7): the user lists show only the company's people, a
/// notification reaches only the project's company, and nobody reads another company's notification.
/// Expectations: docs/multi-tenancy.md → Testler, Bildirim. Cross test (PLANNING.md): Erva → Baran's modules.
/// </summary>
[Collection("Database")]
public class IdentityNotificationIsolationTests(DatabaseFixture database)
{
    private static readonly string[] IthinkaSeedEmails =
        [TestProjects.Analyst, TestProjects.Manager, TestProjects.ProjectManager, TestProjects.Developer, TestProjects.Sales];

    private Task<int> DenemeUserAsync(string email) => TestProjects.UserIdAsync(database, email, database.DenemeId);

    private async Task SendAsync(int tenantId, int actingUserId, Func<INotificationService, Task> send)
    {
        await using var scope = database.CreateScope(tenantId);
        var service = new NotificationService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
            new TestUser(actingUserId, tenantId),
            TimeProvider.System,
            NullLogger<NotificationService>.Instance);
        await send(service);
    }

    private async Task<List<(int UserId, string Message)>> NotificationsAsync(int tenantId, string message)
    {
        await using var scope = database.CreateScope(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        return (await db.Notifications.Where(n => n.Message == message).Select(n => new { n.UserId, n.Message }).ToListAsync())
            .Select(n => (n.UserId, n.Message))
            .ToList();
    }

    // ---- Users ----

    [Fact]
    public async Task UserList_ShowsOnlyTheCompanysPeople()
    {
        var ithinkaAnalystId = await TestProjects.UserIdAsync(database, TestProjects.Analyst);
        var denemeAnalystId = await DenemeUserAsync(TestTenants.DenemeAnalyst);

        await using var ithinkaScope = database.CreateScope();
        var ithinka = await new UserService(ithinkaScope.ServiceProvider.GetRequiredService<IApplicationDbContext>())
            .ListActiveAsync(null, CancellationToken.None);
        await using var denemeScope = database.CreateScope(database.DenemeId);
        var deneme = await new UserService(denemeScope.ServiceProvider.GetRequiredService<IApplicationDbContext>())
            .ListActiveAsync(UserRole.BusinessAnalyst, CancellationToken.None);

        Assert.Contains(ithinka, u => u.Id == ithinkaAnalystId);
        Assert.DoesNotContain(ithinka, u => u.Id == denemeAnalystId);
        Assert.Contains(deneme, u => u.Id == denemeAnalystId);
        Assert.DoesNotContain(deneme, u => u.Id == ithinkaAnalystId);
    }

    [Fact]
    public async Task UserList_OtherCompanysUsersAreNotEvenCountedByEmail()
    {
        await using var scope = database.CreateScope(database.DenemeId);
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        Assert.False(await db.Users.AnyAsync(u => IthinkaSeedEmails.Contains(u.Email)));
    }

    // ---- Notifications ----

    [Fact]
    public async Task NotifyRole_ReachesOnlyTheProjectsCompany()
    {
        // Both companies have a Business Analyst; a Deneme project's notice goes to Deneme's only.
        var projectId = await TestProjects.CreateAsync(database, "5", tenantId: database.DenemeId);
        var managerId = await DenemeUserAsync(TestTenants.DenemeManager);
        var denemeAnalystId = await DenemeUserAsync(TestTenants.DenemeAnalyst);
        var message = $"Rol bildirimi {Guid.NewGuid():N}";

        await SendAsync(database.DenemeId, managerId, s => s.NotifyRoleAsync(UserRole.BusinessAnalyst, projectId, message, CancellationToken.None));

        Assert.Contains((denemeAnalystId, message), await NotificationsAsync(database.DenemeId, message));
        Assert.Empty(await NotificationsAsync(Tenant.IthinkaId, message));
    }

    [Fact]
    public async Task NotifyUsers_AnotherCompanysUser_GetsNothing()
    {
        var projectId = await TestProjects.CreateAsync(database, "5", tenantId: database.DenemeId);
        var managerId = await DenemeUserAsync(TestTenants.DenemeManager);
        var ithinkaAnalystId = await TestProjects.UserIdAsync(database, TestProjects.Analyst);
        var message = $"Kişi bildirimi {Guid.NewGuid():N}";

        await SendAsync(database.DenemeId, managerId, s => s.NotifyUsersAsync([ithinkaAnalystId], projectId, message, CancellationToken.None));

        Assert.Empty(await NotificationsAsync(Tenant.IthinkaId, message));
        Assert.Empty(await NotificationsAsync(database.DenemeId, message));
    }

    [Fact]
    public async Task Inbox_AnotherCompanysNotification_CannotBeMarkedRead()
    {
        var projectId = await TestProjects.CreateAsync(database, "5", tenantId: database.DenemeId);
        var managerId = await DenemeUserAsync(TestTenants.DenemeManager);
        var denemeAnalystId = await DenemeUserAsync(TestTenants.DenemeAnalyst);
        var message = $"Okunacak bildirim {Guid.NewGuid():N}";
        await SendAsync(database.DenemeId, managerId, s => s.NotifyUsersAsync([denemeAnalystId], projectId, message, CancellationToken.None));

        long notificationId;
        await using (var scope = database.CreateScope(database.DenemeId))
        {
            notificationId = await scope.ServiceProvider.GetRequiredService<IApplicationDbContext>().Notifications
                .Where(n => n.Message == message).Select(n => n.Id).SingleAsync();
        }

        var ithinkaAnalystId = await TestProjects.UserIdAsync(database, TestProjects.Analyst);
        await using var ithinkaScope = database.CreateScope();
        var inbox = new NotificationInboxService(
            ithinkaScope.ServiceProvider.GetRequiredService<IApplicationDbContext>(), new TestUser(ithinkaAnalystId), TimeProvider.System);

        await Assert.ThrowsAsync<NotFoundException>(() => inbox.MarkReadAsync(notificationId, CancellationToken.None));
    }
}
