using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Identity;
using Isdlc.Application.Identity.Dtos;
using Isdlc.Application.Tests.Common;
using Isdlc.Domain.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Identity;

/// <summary>GET /api/v1/users: the people to pick in forms (owners, analysts, project managers).</summary>
[Collection("Database")]
public class UserServiceTests(DatabaseFixture database)
{
    private async Task<IReadOnlyList<UserLookupDto>> ListAsync(Permission? permission = null)
    {
        await using var scope = database.CreateScope();
        var service = new UserService(scope.ServiceProvider.GetRequiredService<IApplicationDbContext>());
        return await service.ListActiveAsync(permission, CancellationToken.None);
    }

    [Fact]
    public async Task List_InactiveUser_IsLeftOut()
    {
        var active = await TestUsers.CreateAsync(database, password: null);
        var inactive = await TestUsers.CreateAsync(database, password: null, isActive: false);

        var users = await ListAsync();

        Assert.Contains(users, u => u.Id == active.Id);
        Assert.DoesNotContain(users, u => u.Id == inactive.Id);
    }

    [Fact]
    public async Task List_SeedUsers_AreIncludedWithNameAndPermissions()
    {
        var analystId = await TestProjects.UserIdAsync(database, TestProjects.Analyst);

        var users = await ListAsync();

        var analyst = Assert.Single(users, u => u.Id == analystId);
        Assert.Equal("Elif Yılmaz", analyst.FullName);
        Assert.Equal([Permission.Analysis], analyst.Permissions);
    }

    [Fact]
    public async Task List_IsOrderedByFullName()
    {
        var users = await ListAsync();

        var names = users.Select(u => u.FullName).ToList();
        Assert.Equal(names.OrderBy(n => n, StringComparer.Create(new("tr-TR"), ignoreCase: true)), names);
    }

    [Fact]
    public async Task List_WithPermission_ReturnsOnlyThoseWhoHaveIt()
    {
        var tester = await TestUsers.CreateAsync(database, password: null, permission: Permission.Testing);
        await TestUsers.CreateAsync(database, password: null, isActive: false, permission: Permission.Testing);

        var users = await ListAsync(Permission.Testing);

        Assert.NotEmpty(users);
        Assert.All(users, u => Assert.Contains(Permission.Testing, u.Permissions));
        Assert.Contains(users, u => u.Id == tester.Id);
    }

    [Fact]
    public async Task List_PermissionWithOnlyInactiveNewUsers_ReturnsOnlySeedPeopleWithIt()
    {
        // Seed has exactly one person with Sales; an inactive one must not be added to the list.
        await TestUsers.CreateAsync(database, password: null, isActive: false, permission: Permission.Sales);

        var users = await ListAsync(Permission.Sales);

        var sales = Assert.Single(users);
        Assert.Equal(await TestProjects.UserIdAsync(database, TestProjects.Sales), sales.Id);
    }
}
