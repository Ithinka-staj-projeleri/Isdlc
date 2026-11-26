using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Identity;
using Isdlc.Application.Identity.Dtos;
using Isdlc.Application.Tests.Common;
using Isdlc.Domain.Identity;
using Isdlc.Domain.Tenants;
using Isdlc.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Identity;

/// <summary>
/// The company administrator's user management (Faz 6 · T4) on a real database with two companies.
/// Expectations: docs/multi-tenancy.md → Uç noktalar (Şirket Yöneticisi), Hata mesajları.
/// </summary>
[Collection("Database")]
public class UserAdminServiceTests(DatabaseFixture database)
{
    private const string Temporary = "Gecici-Parola-2026";

    private sealed class RecordingCache : IAccountStatusCache
    {
        public List<(int UserId, int? TenantId)> Forgotten { get; } = [];

        public void Forget(int userId, int? tenantId) => Forgotten.Add((userId, tenantId));
    }

    /// <summary>The service as one user of a company: the scope and the signed-in user are both that company's.</summary>
    private async Task<T> AsAsync<T>(
        int userId, Func<IUserAdminService, Task<T>> action, int tenantId = Tenant.IthinkaId, bool isTenantAdmin = true,
        RecordingCache? cache = null)
    {
        await using var scope = database.CreateScope(tenantId);
        var service = new UserAdminService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
            new TestUser(userId, tenantId, isTenantAdmin),
            scope.ServiceProvider.GetRequiredService<IPasswordHasher>(),
            cache ?? new RecordingCache(),
            TimeProvider.System);
        return await action(service);
    }

    private Task<User> AdminAsync(int? tenantId = null) =>
        TestUsers.CreateAsync(database, Temporary, isTenantAdmin: true, tenantId: tenantId);

    private static CreateUserDto NewUser(Permission? permission = Permission.Development, string? email = null) => new()
    {
        FullName = "  Yeni Kullanıcı  ",
        Email = email ?? $"yeni.{Guid.NewGuid():N}@isdlc.local",
        Permissions = permission is { } one ? [one] : [],
        TemporaryPassword = Temporary,
    };

    /// <summary>The user as they are, with the given changes; the permission list is sent whole, as the screen does.</summary>
    private static UpdateUserDto Edit(
        User user, bool? isActive = null, bool? isTenantAdmin = null, List<Permission>? permissions = null) => new()
    {
        FullName = user.FullName,
        Permissions = permissions ?? user.Permissions.Select(p => p.Permission).ToList(),
        IsActive = isActive ?? user.IsActive,
        IsTenantAdmin = isTenantAdmin ?? user.IsTenantAdmin,
    };

    /// <summary>The user's saved permissions, in the list's order.</summary>
    private async Task<List<Permission>> PermissionsOfAsync(int userId)
    {
        await using var scope = database.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IApplicationDbContext>().UserPermissions
            .IgnoreQueryFilters() // tenant: a test reads any company's rows by the user id
            .Where(p => p.UserId == userId)
            .Select(p => p.Permission)
            .OrderBy(p => p)
            .ToListAsync();
    }

    /// <summary>A company of this test only, so "the company's only administrator" is not affected by other tests.</summary>
    private async Task<int> NewCompanyAsync()
    {
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();
        var tenant = new Tenant { Code = $"yonetim-{Guid.NewGuid():N}"[..20], Name = "Yönetim A.Ş.", CreatedAt = DateTime.UtcNow };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        return tenant.Id;
    }

    private async Task<User> ReadAsync(int userId)
    {
        await using var scope = database.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IsdlcDbContext>().Users
            .IgnoreQueryFilters().AsNoTracking().SingleAsync(u => u.Id == userId);
    }

    // ---- Who may ----

    [Fact]
    public async Task EveryCall_NotATenantAdmin_IsForbidden()
    {
        var user = await TestUsers.CreateAsync(database, Temporary);
        const string message = "Bu işlem için şirket yöneticisi olmalısınız.";

        Assert.Equal(message, (await Assert.ThrowsAsync<ForbiddenException>(() => AsAsync(user.Id, s => s.ListAsync(default), isTenantAdmin: false))).Message);
        await Assert.ThrowsAsync<ForbiddenException>(() => AsAsync(user.Id, s => s.CreateAsync(NewUser(), default), isTenantAdmin: false));
        await Assert.ThrowsAsync<ForbiddenException>(() => AsAsync(user.Id, s => s.UpdateAsync(user.Id, Edit(user), default), isTenantAdmin: false));
        await Assert.ThrowsAsync<ForbiddenException>(() => AsAsync(user.Id, async s =>
        {
            await s.ResetPasswordAsync(user.Id, new ResetPasswordDto { TemporaryPassword = Temporary }, default);
            return 0;
        }, isTenantAdmin: false));
    }

    // ---- List ----

    [Fact]
    public async Task List_OwnCompanyOnly_InactiveUsersIncluded()
    {
        var admin = await AdminAsync();
        var inactive = await TestUsers.CreateAsync(database, null, isActive: false);
        var denemeUser = await TestUsers.CreateAsync(database, null, tenantId: database.DenemeId);

        var users = await AsAsync(admin.Id, s => s.ListAsync(default));

        Assert.Contains(users, u => u.Id == inactive.Id && !u.IsActive);
        Assert.Contains(users, u => u.Id == admin.Id && u.IsTenantAdmin);
        Assert.DoesNotContain(users, u => u.Id == denemeUser.Id);
    }

    // ---- Create ----

    [Fact]
    public async Task Create_SavesToTheAdminsCompanyWithATemporaryPassword()
    {
        var admin = await AdminAsync(database.DenemeId);

        var created = await AsAsync(admin.Id, s => s.CreateAsync(NewUser(Permission.ProjectManagement), default), database.DenemeId);

        Assert.Equal(("Yeni Kullanıcı", true, true, false),
            (created.FullName, created.IsActive, created.MustChangePassword, created.IsTenantAdmin));
        Assert.Equal([Permission.ProjectManagement], created.Permissions);
        var saved = await ReadAsync(created.Id);
        Assert.Equal(database.DenemeId, saved.TenantId);
        await using var scope = database.CreateScope();
        Assert.True(scope.ServiceProvider.GetRequiredService<IPasswordHasher>().Verify(saved.PasswordHash, Temporary));
    }

    [Fact]
    public async Task Create_EmailOfAUserInAnotherCompany_IsAConflict()
    {
        var admin = await AdminAsync();
        var denemeUser = await TestUsers.CreateAsync(database, null, tenantId: database.DenemeId);

        var error = await Assert.ThrowsAsync<ConflictException>(() =>
            AsAsync(admin.Id, s => s.CreateAsync(NewUser(email: denemeUser.Email), default)));

        Assert.Equal("Bu e-posta ile bir kullanıcı zaten var.", error.Message);
    }

    [Fact]
    public async Task Create_SeveralOrNoPermissions_AreSavedAsASet()
    {
        var admin = await AdminAsync();
        var several = NewUser();
        several.Permissions = [Permission.Testing, Permission.Analysis, Permission.Testing];

        var withSeveral = await AsAsync(admin.Id, s => s.CreateAsync(several, default));
        var withNone = await AsAsync(admin.Id, s => s.CreateAsync(NewUser(permission: null), default));

        Assert.Equal([Permission.Analysis, Permission.Testing], withSeveral.Permissions);
        Assert.Empty(withNone.Permissions);
        Assert.Equal([Permission.Analysis, Permission.Testing], await PermissionsOfAsync(withSeveral.Id));
    }

    // ---- Update ----

    [Fact]
    public async Task Update_ChangesNamePermissionsAndMarks_NeverTheEmail()
    {
        var admin = await AdminAsync();
        var user = await TestUsers.CreateAsync(database, null);

        var updated = await AsAsync(admin.Id, s => s.UpdateAsync(user.Id, new UpdateUserDto
        {
            FullName = "Yeni Ad", Permissions = [Permission.Testing], IsActive = true, IsTenantAdmin = true,
        }, default));

        Assert.Equal(("Yeni Ad", true, user.Email), (updated.FullName, updated.IsTenantAdmin, updated.Email));
        Assert.Equal([Permission.Testing], updated.Permissions);
    }

    [Fact]
    public async Task Update_Permissions_AddsAndTakesAwayToMatchTheList()
    {
        var admin = await AdminAsync();
        var user = await TestUsers.CreateAsync(
            database, null, permissions: [Permission.Analysis, Permission.Development]);

        await AsAsync(admin.Id, s => s.UpdateAsync(
            user.Id, Edit(user, permissions: [Permission.Development, Permission.Approval]), default));
        Assert.Equal([Permission.Approval, Permission.Development], await PermissionsOfAsync(user.Id));

        await AsAsync(admin.Id, s => s.UpdateAsync(user.Id, Edit(user, permissions: []), default));
        Assert.Empty(await PermissionsOfAsync(user.Id));
    }

    [Fact]
    public async Task Update_PermissionsNotSent_LeavesThemAsTheyAre()
    {
        // A form without the permission boxes (the screen before A3) must not take them away.
        var admin = await AdminAsync();
        var user = await TestUsers.CreateAsync(database, null, permission: Permission.Sales);

        var update = Edit(user);
        update.Permissions = null;
        await AsAsync(admin.Id, s => s.UpdateAsync(user.Id, update, default));

        Assert.Equal([Permission.Sales], await PermissionsOfAsync(user.Id));
    }

    [Fact]
    public async Task Update_AdministratorGivesThemselvesAPermission_IsAllowed()
    {
        var admin = await AdminAsync();

        var updated = await AsAsync(admin.Id, s => s.UpdateAsync(
            admin.Id, Edit(admin, permissions: [Permission.Approval]), default));

        Assert.Equal([Permission.Approval], updated.Permissions);
        Assert.True(updated.IsTenantAdmin);
    }

    [Fact]
    public async Task Update_DeactivatingOneself_IsRefused()
    {
        var admin = await AdminAsync();
        await AdminAsync(); // another administrator, so only the "oneself" rule applies

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            AsAsync(admin.Id, s => s.UpdateAsync(admin.Id, Edit(admin, isActive: false), default)));

        Assert.Equal("Kendi hesabınızı pasife alamazsınız.", error.Message);
    }

    [Fact]
    public async Task Update_LastActiveAdmin_KeepsTheMark_ASecondOneMayGo()
    {
        var companyId = await NewCompanyAsync();
        var first = await AdminAsync(companyId);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            AsAsync(first.Id, s => s.UpdateAsync(first.Id, Edit(first, isTenantAdmin: false), default), companyId));
        Assert.Equal("Şirketin en az bir aktif yöneticisi olmalıdır.", error.Message);

        var second = await AdminAsync(companyId);
        var updated = await AsAsync(first.Id, s => s.UpdateAsync(second.Id, Edit(second, isActive: false), default), companyId);
        Assert.False(updated.IsActive);

        // Now the first one is again the only active administrator.
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            AsAsync(second.Id, s => s.UpdateAsync(first.Id, Edit(first, isTenantAdmin: false), default), companyId));
    }

    [Fact]
    public async Task Update_UserOfAnotherCompany_IsNotFound()
    {
        var admin = await AdminAsync();
        var denemeUser = await TestUsers.CreateAsync(database, null, tenantId: database.DenemeId);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            AsAsync(admin.Id, s => s.UpdateAsync(denemeUser.Id, Edit(denemeUser, isActive: false), default)));
        Assert.True((await ReadAsync(denemeUser.Id)).IsActive);
    }

    [Fact]
    public async Task Update_Deactivated_IsSignedOutAtOnce()
    {
        var admin = await AdminAsync();
        var user = await TestUsers.CreateAsync(database, null);
        var cache = new RecordingCache();

        await AsAsync(admin.Id, s => s.UpdateAsync(user.Id, Edit(user, isActive: false), default), cache: cache);

        Assert.Equal([(user.Id, (int?)Tenant.IthinkaId)], cache.Forgotten);
    }

    // ---- Reset password ----

    [Fact]
    public async Task ResetPassword_GivesATemporaryPassword()
    {
        var admin = await AdminAsync();
        var user = await TestUsers.CreateAsync(database, "Eski-Parola-12345");
        const string reset = "Sifirlanan-Parola-1";

        await AsAsync(admin.Id, async s =>
        {
            await s.ResetPasswordAsync(user.Id, new ResetPasswordDto { TemporaryPassword = reset }, default);
            return 0;
        });

        var saved = await ReadAsync(user.Id);
        Assert.True(saved.MustChangePassword);
        await using var scope = database.CreateScope();
        Assert.True(scope.ServiceProvider.GetRequiredService<IPasswordHasher>().Verify(saved.PasswordHash, reset));
    }
}
