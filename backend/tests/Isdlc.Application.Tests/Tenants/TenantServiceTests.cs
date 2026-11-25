using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Common.Models;
using Isdlc.Application.Identity;
using Isdlc.Application.Tenants;
using Isdlc.Application.Tenants.Dtos;
using Isdlc.Application.Tests.Common;
using Isdlc.Application.Tests.Identity;
using Isdlc.Domain.Identity;
using Isdlc.Domain.Tenants;
using Isdlc.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Tenants;

/// <summary>
/// Companies (Faz 6 · T4) on a real database: the Platform Administrator's list, opening a company with its template
/// copy and first administrator in one transaction, editing, and every user's current company.
/// Expectations: docs/multi-tenancy.md → Uç noktalar (Platform Yöneticisi), Seed ve şablon, Hata mesajları.
/// </summary>
[Collection("Database")]
public class TenantServiceTests(DatabaseFixture database)
{
    private const string Temporary = "Gecici-Parola-2026";

    private sealed class FailingTemplates : ITenantTemplates
    {
        public Task ApplyAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Şablon kopyası başarısız (test).");
    }

    /// <summary>
    /// The service as the Platform Administrator: no company in the scope or the user. templates replaces the real
    /// template copy when given.
    /// </summary>
    private async Task<T> AsPlatformAsync<T>(Func<ITenantService, ITenantContext, Task<T>> action, ITenantTemplates? templates = null)
    {
        await using var scope = database.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        context.Use(null);
        var service = Service(scope, new TestUser(1, isPlatformAdmin: true), templates);
        return await action(service, context);
    }

    private static TenantService Service(AsyncServiceScope scope, ICurrentUserService user, ITenantTemplates? templates = null) => new(
        scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
        user,
        scope.ServiceProvider.GetRequiredService<ITenantContext>(),
        templates ?? scope.ServiceProvider.GetRequiredService<ITenantTemplates>(),
        scope.ServiceProvider.GetRequiredService<IPasswordHasher>(),
        TimeProvider.System);

    private static string NewCode() => $"sirket-{Guid.NewGuid():N}"[..20];

    private static CreateTenantDto NewTenant(string? code = null, string? email = null) => new()
    {
        Code = code ?? NewCode(),
        Name = "  Örnek Lojistik A.Ş.  ",
        Admin = new TenantAdminDto
        {
            FullName = "Deniz Aydın",
            Email = email ?? $"deniz.{Guid.NewGuid():N}@ornek.local",
            Role = UserRole.BusinessAnalyst,
            TemporaryPassword = Temporary,
        },
    };

    private async Task<T> InCompanyAsync<T>(int tenantId, Func<IsdlcDbContext, Task<T>> read)
    {
        await using var scope = database.CreateScope(tenantId);
        return await read(scope.ServiceProvider.GetRequiredService<IsdlcDbContext>());
    }

    // ---- Who may ----

    [Fact]
    public async Task PlatformCalls_CompanyAdmin_IsForbidden()
    {
        await using var scope = database.CreateScope();
        var service = Service(scope, new TestUser(1, isTenantAdmin: true));
        const string message = "Bu işlem yalnızca platform yöneticisine açıktır.";

        Assert.Equal(message, (await Assert.ThrowsAsync<ForbiddenException>(() => service.ListAsync(new PageRequest(), default))).Message);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateAsync(NewTenant(), default));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.UpdateAsync(Tenant.IthinkaId, new UpdateTenantDto { Name = "x", IsActive = false }, default));
    }

    // ---- Open a company ----

    [Fact]
    public async Task Create_CopiesTheTemplatesAndOpensTheFirstAdministrator()
    {
        var request = NewTenant();

        var (tenant, contextAfter) = await AsPlatformAsync(async (s, context) =>
            (await s.CreateAsync(request, default), context.TenantId));

        Assert.Equal((request.Code, "Örnek Lojistik A.Ş.", true, 1), (tenant.Code, tenant.Name, tenant.IsActive, tenant.UserCount));
        // The request goes back to working as nobody's company.
        Assert.Null(contextAfter);

        var (flows, sets, documentTypes, admin) = await InCompanyAsync(tenant.Id, async db => (
            await db.ProcessFlows.CountAsync(),
            await db.QuestionSets.CountAsync(),
            await db.DocumentTypes.CountAsync(),
            await db.Users.SingleAsync()));
        var ithinkaFlows = await InCompanyAsync(Tenant.IthinkaId, db => db.ProcessFlows.CountAsync(f => f.TemplateCode != null));
        Assert.Equal(ithinkaFlows, flows);
        Assert.True(sets > 0 && documentTypes > 0);
        Assert.Equal((request.Admin!.Email, UserRole.BusinessAnalyst, true, true),
            (admin.Email, admin.Role, admin.IsTenantAdmin, admin.MustChangePassword));
    }

    [Fact]
    public async Task Create_TemplateCopyFails_NothingIsSaved()
    {
        var request = NewTenant();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AsPlatformAsync((s, _) => s.CreateAsync(request, default), new FailingTemplates()));

        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();
        Assert.False(await db.Tenants.AnyAsync(t => t.Code == request.Code));
        Assert.False(await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == request.Admin!.Email));
    }

    [Theory]
    [InlineData("A")]
    [InlineData("Büyük")]
    [InlineData("ornek_lojistik")]
    [InlineData("-ornek")]
    [InlineData("ornek--lojistik")]
    public async Task Create_BadCode_IsRefused(string code)
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            AsPlatformAsync((s, _) => s.CreateAsync(NewTenant(code), default)));

        Assert.Equal("Şirket kodu küçük harf, rakam ve tireden oluşmalıdır (2–50 karakter).", error.Message);
    }

    [Fact]
    public async Task Create_CodeInUse_IsAConflict()
    {
        var error = await Assert.ThrowsAsync<ConflictException>(() =>
            AsPlatformAsync((s, _) => s.CreateAsync(NewTenant(TestTenants.DenemeCode), default)));

        Assert.Equal("Bu şirket kodu kullanılıyor.", error.Message);
    }

    [Fact]
    public async Task Create_AdminEmailOfAnExistingUser_IsAConflictAndOpensNothing()
    {
        var existing = await TestUsers.CreateAsync(database, null);
        var request = NewTenant(email: existing.Email);

        var error = await Assert.ThrowsAsync<ConflictException>(() => AsPlatformAsync((s, _) => s.CreateAsync(request, default)));

        Assert.Equal("Bu e-posta ile bir kullanıcı zaten var.", error.Message);
        await using var scope = database.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<IsdlcDbContext>().Tenants.AnyAsync(t => t.Code == request.Code));
    }

    // ---- List and edit ----

    [Fact]
    public async Task List_EveryCompanyWithItsUserCount_InactiveToo()
    {
        var opened = await AsPlatformAsync((s, _) => s.CreateAsync(NewTenant(), default));
        await AsPlatformAsync((s, _) => s.UpdateAsync(opened.Id, new UpdateTenantDto { Name = opened.Name, IsActive = false }, default));

        var list = await AsPlatformAsync((s, _) => s.ListAsync(new PageRequest { Page = 1, PageSize = 100 }, default));

        Assert.Contains(list.Items, t => t.Id == opened.Id && !t.IsActive && t.UserCount == 1);
        Assert.Contains(list.Items, t => t.Id == Tenant.IthinkaId && t.UserCount > 1);
    }

    [Fact]
    public async Task Update_NameAndActive_CodeNeverChanges()
    {
        var opened = await AsPlatformAsync((s, _) => s.CreateAsync(NewTenant(), default));

        var updated = await AsPlatformAsync((s, _) =>
            s.UpdateAsync(opened.Id, new UpdateTenantDto { Name = "  Yeni Ad  ", IsActive = false }, default));

        Assert.Equal((opened.Code, "Yeni Ad", false), (updated.Code, updated.Name, updated.IsActive));
    }

    [Fact]
    public async Task Update_UnknownCompany_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            AsPlatformAsync((s, _) => s.UpdateAsync(int.MaxValue, new UpdateTenantDto { Name = "x" }, default)));
    }

    // ---- Current company ----

    [Fact]
    public async Task GetCurrent_CompanyUser_GetsTheirCompany_PlatformAdminHasNone()
    {
        await using var scope = database.CreateScope(database.DenemeId);
        var current = await Service(scope, new TestUser(1, database.DenemeId)).GetCurrentAsync(default);
        Assert.Equal((database.DenemeId, TestTenants.DenemeCode, TestTenants.DenemeName), (current.Id, current.Code, current.Name));

        await Assert.ThrowsAsync<NotFoundException>(() => AsPlatformAsync((s, _) => s.GetCurrentAsync(default)));
    }

    // ---- Code rule ----

    [Theory]
    [InlineData("ithinka", true)]
    [InlineData("ornek-lojistik", true)]
    [InlineData("a1", true)]
    [InlineData("a", false)]
    [InlineData("ornek-", false)]
    [InlineData("Ornek", false)]
    [InlineData("örnek", false)]
    public void TenantCodeRules_IsValid(string code, bool expected)
    {
        Assert.Equal(expected, TenantCodeRules.IsValid(code));
    }

    [Fact]
    public void TenantCodeRules_LongerThan50_IsInvalid()
    {
        Assert.False(TenantCodeRules.IsValid(new string('a', 51)));
        Assert.True(TenantCodeRules.IsValid(new string('a', 50)));
    }
}
