using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Identity;

/// <summary>Ithinka's seed users (Seed/users.json): the company has an administrator from the start.</summary>
[Collection("Database")]
public class SeedUsersTests(DatabaseFixture database)
{
    [Fact]
    public async Task SeedUsers_IthinkaHasOnlyTheMarkedCompanyAdministrator()
    {
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var seedEmails = new[]
        {
            TestProjects.Analyst, TestProjects.Manager, TestProjects.ProjectManager,
        };
        var admins = await db.Users.AsNoTracking()
            .Where(u => seedEmails.Contains(u.Email))
            .ToDictionaryAsync(u => u.Email, u => u.IsTenantAdmin);

        Assert.True(admins[TestProjects.Manager]);
        Assert.False(admins[TestProjects.Analyst]);
        Assert.False(admins[TestProjects.ProjectManager]);
    }
}
