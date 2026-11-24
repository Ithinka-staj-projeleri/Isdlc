using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Identity;
using Isdlc.Application.Tests.Common;
using Isdlc.Domain.Identity;
using Isdlc.Domain.Tenants;
using Isdlc.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Identity;

/// <summary>
/// The check made on every request (docs/multi-tenancy.md → İstek başına): the user is active, still of the company in
/// their token, and the company is active. The API caches the answer for a minute; here the service is asked directly.
/// </summary>
[Collection("Database")]
public class AccountStatusServiceTests(DatabaseFixture database)
{
    private async Task<bool> IsActiveAsync(int userId, int? tenantId)
    {
        // Asked as an anonymous request would be: the scope is Ithinka's, but the check does not depend on it.
        await using var scope = database.CreateScope();
        var service = new AccountStatusService(scope.ServiceProvider.GetRequiredService<IApplicationDbContext>());
        return await service.IsActiveAsync(userId, tenantId, CancellationToken.None);
    }

    private async Task<int> CreateTenantAsync(bool isActive)
    {
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();
        var tenant = new Tenant
        {
            Code = $"durum-{Guid.NewGuid():N}"[..20], Name = "Durum A.Ş.", IsActive = isActive, CreatedAt = DateTime.UtcNow,
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        return tenant.Id;
    }

    [Fact]
    public async Task IsActive_ActiveUserOfAnActiveCompany_IsTrue()
    {
        var user = await TestUsers.CreateAsync(database, password: null);

        Assert.True(await IsActiveAsync(user.Id, Tenant.IthinkaId));
    }

    [Fact]
    public async Task IsActive_DeactivatedUser_IsFalse()
    {
        var user = await TestUsers.CreateAsync(database, password: null, isActive: false);

        Assert.False(await IsActiveAsync(user.Id, Tenant.IthinkaId));
    }

    [Fact]
    public async Task IsActive_CompanyDeactivatedAfterSignIn_IsFalse()
    {
        var tenantId = await CreateTenantAsync(isActive: true);
        var user = await TestUsers.CreateAsync(database, password: null, tenantId: tenantId);
        Assert.True(await IsActiveAsync(user.Id, tenantId));

        await using (var scope = database.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();
            (await db.Tenants.SingleAsync(t => t.Id == tenantId)).IsActive = false;
            await db.SaveChangesAsync();
        }

        Assert.False(await IsActiveAsync(user.Id, tenantId));
    }

    [Fact]
    public async Task IsActive_TokenOfAnotherCompany_IsFalse()
    {
        var user = await TestUsers.CreateAsync(database, password: null);
        var otherTenantId = await CreateTenantAsync(isActive: true);

        Assert.False(await IsActiveAsync(user.Id, otherTenantId));
        // A token from before Phase 6 has no company.
        Assert.False(await IsActiveAsync(user.Id, null));
    }

    [Fact]
    public async Task IsActive_PlatformAdminWithoutACompany_IsTrue()
    {
        var admin = await TestUsers.CreateAsync(database, password: null, role: UserRole.PlatformAdmin);

        Assert.True(await IsActiveAsync(admin.Id, null));
        Assert.False(await IsActiveAsync(admin.Id, Tenant.IthinkaId));
    }

    [Fact]
    public async Task IsActive_UnknownUser_IsFalse()
    {
        Assert.False(await IsActiveAsync(int.MaxValue, Tenant.IthinkaId));
    }

    // ---- The rule in the database (docs/multi-tenancy.md → Users) ----

    [Fact]
    public async Task Save_PlatformAdminMarkedTenantAdmin_IsRefusedByTheDatabase()
    {
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();
        db.Users.Add(new User
        {
            FullName = "Platform", Email = $"platform.{Guid.NewGuid():N}@isdlc.local", Role = UserRole.PlatformAdmin,
            IsTenantAdmin = true, CreatedAt = DateTime.UtcNow,
        });

        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Contains("CK_Users_TenantByRole", error.InnerException?.Message);
    }

    [Fact]
    public async Task Save_PlatformAdminGivenACompany_IsRefusedBeforeTheDatabase()
    {
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();
        db.Users.Add(new User
        {
            TenantId = Tenant.IthinkaId, FullName = "Platform", Email = $"platform.{Guid.NewGuid():N}@isdlc.local",
            Role = UserRole.PlatformAdmin, CreatedAt = DateTime.UtcNow,
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Save_PlatformAdminWhileNoCompanyIsKnown_IsAllowed()
    {
        // The seed opens the Platform Administrator with no company set (docs/multi-tenancy.md → Seed, step 4).
        await using var scope = database.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(null);
        var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();
        var admin = new User
        {
            FullName = "Platform", Email = $"platform.{Guid.NewGuid():N}@isdlc.local", Role = UserRole.PlatformAdmin,
            CreatedAt = DateTime.UtcNow,
        };
        db.Users.Add(admin);

        await db.SaveChangesAsync();

        Assert.Null(admin.TenantId);
    }
}
