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
    private async Task<IReadOnlyList<UserLookupDto>> ListAsync(UserRole? role = null)
    {
        await using var scope = database.CreateScope();
        var service = new UserService(scope.ServiceProvider.GetRequiredService<IApplicationDbContext>());
        return await service.ListActiveAsync(role, CancellationToken.None);
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
    public async Task List_SeedUsers_AreIncludedWithNameAndRole()
    {
        var analystId = await TestProjects.UserIdAsync(database, TestProjects.Analyst);

        var users = await ListAsync();

        Assert.Contains(new UserLookupDto(analystId, "Elif Yılmaz", UserRole.BusinessAnalyst), users);
    }

    [Fact]
    public async Task List_IsOrderedByFullName()
    {
        var users = await ListAsync();

        var names = users.Select(u => u.FullName).ToList();
        Assert.Equal(names.OrderBy(n => n, StringComparer.Create(new("tr-TR"), ignoreCase: true)), names);
    }

    [Fact]
    public async Task List_WithRole_ReturnsOnlyThatRole()
    {
        var tester = await TestUsers.CreateAsync(database, password: null, role: UserRole.TestSpecialist);
        await TestUsers.CreateAsync(database, password: null, isActive: false, role: UserRole.TestSpecialist);

        var users = await ListAsync(UserRole.TestSpecialist);

        Assert.NotEmpty(users);
        Assert.All(users, u => Assert.Equal(UserRole.TestSpecialist, u.Role));
        Assert.Contains(users, u => u.Id == tester.Id);
    }

    [Fact]
    public async Task List_RoleWithOnlyInactiveUsers_ReturnsOnlySeedPeopleOfThatRole()
    {
        // Seed has exactly one Sales Representative; an inactive one must not be added to the list.
        await TestUsers.CreateAsync(database, password: null, isActive: false, role: UserRole.SalesRepresentative);

        var users = await ListAsync(UserRole.SalesRepresentative);

        var sales = Assert.Single(users);
        Assert.Equal(await TestProjects.UserIdAsync(database, TestProjects.Sales), sales.Id);
    }
}
