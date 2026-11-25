using System.ComponentModel.DataAnnotations;
using Isdlc.Application.Agents;
using Isdlc.Application.Agents.Dtos;
using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Tenants;
using Isdlc.Application.Tenants.Dtos;
using Isdlc.Application.Tests.Agents;
using Isdlc.Application.Tests.Common;
using Isdlc.Application.Tests.Identity;
using Isdlc.Domain.Tenants;
using Isdlc.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Tenants;

/// <summary>
/// GET and PUT /tenants/current/settings (Faz 6 · T6): only the company administrator reads and changes the AI switches,
/// and only those of their own company. Each test opens its own company, so Ithinka and Deneme are never changed.
/// Expectations: docs/multi-tenancy.md → Şirket Yöneticisi.
/// The tests under "Cross tests" are the cross test (PLANNING.md): Dide → Erva's modules.
/// </summary>
[Collection("Database")]
public class TenantSettingsServiceTests(DatabaseFixture database)
{
    private record Company(int TenantId, int AdminId, int MemberId);

    private async Task<Company> CreateCompanyAsync(bool aiEnabled = false, bool dataSharing = false)
    {
        int tenantId;
        await using (var scope = database.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();
            var tenant = new Tenant
            {
                Code = $"ayar-{Guid.NewGuid():N}"[..20], Name = "Ayar A.Ş.", CreatedAt = DateTime.UtcNow,
                AiEnabled = aiEnabled, AiDataSharingAllowed = dataSharing,
            };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();
            tenantId = tenant.Id;
        }

        var admin = await TestUsers.CreateAsync(database, null, tenantId: tenantId, isTenantAdmin: true);
        var member = await TestUsers.CreateAsync(database, null, tenantId: tenantId);
        return new Company(tenantId, admin.Id, member.Id);
    }

    private async Task<T> AsAsync<T>(ICurrentUserService user, int tenantId, Func<TenantSettingsService, Task<T>> action)
    {
        await using var scope = database.CreateScope(tenantId);
        return await action(new TenantSettingsService(scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(), user));
    }

    private Task<T> AsAdminAsync<T>(Company company, Func<TenantSettingsService, Task<T>> action) =>
        AsAsync(new TestUser(company.AdminId, company.TenantId, isTenantAdmin: true), company.TenantId, action);

    private async Task<Tenant> TenantAsync(int tenantId)
    {
        await using var scope = database.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IsdlcDbContext>().Tenants.AsNoTracking().SingleAsync(t => t.Id == tenantId);
    }

    private static UpdateTenantSettingsDto Update(bool aiEnabled, bool dataSharing) =>
        new() { AiEnabled = aiEnabled, AiDataSharingAllowed = dataSharing };

    [Fact]
    public async Task Get_TenantAdmin_ReturnsTheCompanysSwitches()
    {
        var company = await CreateCompanyAsync(aiEnabled: true, dataSharing: false);

        var settings = await AsAdminAsync(company, s => s.GetAsync(CancellationToken.None));

        Assert.Equal(new TenantSettingsDto(true, false), settings);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task Update_TenantAdmin_SavesBothSwitches(bool aiEnabled, bool dataSharing)
    {
        var company = await CreateCompanyAsync(aiEnabled: !aiEnabled, dataSharing: !dataSharing);

        var saved = await AsAdminAsync(company, s => s.UpdateAsync(Update(aiEnabled, dataSharing), CancellationToken.None));

        Assert.Equal(new TenantSettingsDto(aiEnabled, dataSharing), saved);
        var tenant = await TenantAsync(company.TenantId);
        Assert.Equal(aiEnabled, tenant.AiEnabled);
        Assert.Equal(dataSharing, tenant.AiDataSharingAllowed);
    }

    [Fact]
    public async Task Update_ChangesOnlyTheAdminsOwnCompany()
    {
        var company = await CreateCompanyAsync();
        var other = await CreateCompanyAsync();

        await AsAdminAsync(company, s => s.UpdateAsync(Update(true, true), CancellationToken.None));

        var untouched = await TenantAsync(other.TenantId);
        Assert.False(untouched.AiEnabled);
        Assert.False(untouched.AiDataSharingAllowed);
    }

    [Fact]
    public async Task Update_TheAssistantsStatusFollowsTheNewSwitches()
    {
        // AI on, no data sharing: only the recording assistant shows (docs/multi-tenancy.md → Yapay zekâ ayarlarının etkisi).
        var company = await CreateCompanyAsync();
        await AsAdminAsync(company, s => s.UpdateAsync(Update(true, false), CancellationToken.None));

        await using var scope = database.CreateScope(company.TenantId);
        var agents = new AgentService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
            new TestUser(company.MemberId, company.TenantId),
            new FakeAgentClient { IsAvailable = true, IsTranscriptionAvailable = true });
        var status = await agents.GetStatusAsync(CancellationToken.None);

        Assert.False(status.MeetingAssistantEnabled);
        Assert.True(status.TranscriptionEnabled);
    }

    [Fact]
    public async Task GetAndUpdate_NotTenantAdmin_AreForbiddenAndNothingChanges()
    {
        var company = await CreateCompanyAsync();
        var member = new TestUser(company.MemberId, company.TenantId);

        var getError = await Assert.ThrowsAsync<ForbiddenException>(
            () => AsAsync(member, company.TenantId, s => s.GetAsync(CancellationToken.None)));
        var updateError = await Assert.ThrowsAsync<ForbiddenException>(
            () => AsAsync(member, company.TenantId, s => s.UpdateAsync(Update(true, true), CancellationToken.None)));

        Assert.Equal("Bu işlem için şirket yöneticisi olmalısınız.", getError.Message);
        Assert.Equal("Bu işlem için şirket yöneticisi olmalısınız.", updateError.Message);
        Assert.False((await TenantAsync(company.TenantId)).AiEnabled);
    }

    [Fact]
    public async Task Update_PlatformAdmin_HasNoCompanyAndIsForbidden()
    {
        var company = await CreateCompanyAsync();
        var platformAdmin = new TestUser(company.AdminId, isPlatformAdmin: true);

        var error = await Assert.ThrowsAsync<ForbiddenException>(
            () => AsAsync(platformAdmin, company.TenantId, s => s.UpdateAsync(Update(true, true), CancellationToken.None)));

        Assert.Equal(TenantSettingsService.TenantAdminRequired, error.Message);
    }

    [Fact]
    public async Task Get_NotSignedIn_IsForbidden()
    {
        await Assert.ThrowsAsync<ForbiddenException>(
            () => AsAsync(new TestUser(null), Tenant.IthinkaId, s => s.GetAsync(CancellationToken.None)));
    }

    [Fact]
    public void UpdateDto_MissingSwitches_AreTurkishErrors()
    {
        var dto = new UpdateTenantSettingsDto();
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);

        Assert.Equal(
            ["Yapay zekâ ayarı belirtilmelidir.", "Veri gönderme izni belirtilmelidir."],
            results.Select(r => r.ErrorMessage));
    }

    // ---- Cross tests (PLANNING.md): Dide → Erva's modules ----

    [Fact]
    public async Task Get_ReturnsOnlyTheAdminsOwnCompany()
    {
        // The other company's switches are the opposite; the address has no id, so only the token's company can answer.
        var company = await CreateCompanyAsync(aiEnabled: false, dataSharing: false);
        await CreateCompanyAsync(aiEnabled: true, dataSharing: true);

        var settings = await AsAdminAsync(company, s => s.GetAsync(CancellationToken.None));

        Assert.Equal(new TenantSettingsDto(false, false), settings);
    }

    [Fact]
    public async Task Update_KeepsTheCompanysOtherFields()
    {
        var company = await CreateCompanyAsync();
        var before = await TenantAsync(company.TenantId);

        await AsAdminAsync(company, s => s.UpdateAsync(Update(true, true), CancellationToken.None));

        var after = await TenantAsync(company.TenantId);
        Assert.Equal(before.Code, after.Code);
        Assert.Equal(before.Name, after.Name);
        Assert.Equal(before.IsActive, after.IsActive);
        Assert.Equal(before.CreatedAt, after.CreatedAt);
    }

    [Fact]
    public async Task Update_TurningOff_TheNextStatusIsOff()
    {
        // On, then off again: the status reads the company on every request, nothing keeps the old "on".
        var company = await CreateCompanyAsync();
        await AsAdminAsync(company, s => s.UpdateAsync(Update(true, true), CancellationToken.None));
        Assert.Equal(new AgentStatusDto(true, true, true, true), await StatusAsync(company));

        await AsAdminAsync(company, s => s.UpdateAsync(Update(false, true), CancellationToken.None));

        // Data sharing alone keeps every assistant off.
        Assert.Equal(new AgentStatusDto(false, false, false, false), await StatusAsync(company));
    }

    [Fact]
    public async Task Update_IsNotWrittenToTheAuditLog()
    {
        // docs/multi-tenancy.md: Tenant is not an audited record.
        var company = await CreateCompanyAsync();
        var before = await TenantAuditLogCountAsync();

        await AsAdminAsync(company, s => s.UpdateAsync(Update(true, true), CancellationToken.None));

        Assert.Equal(before, await TenantAuditLogCountAsync());
    }

    [Fact]
    public async Task Get_PlatformAdmin_HasNoCompanyAndIsForbidden()
    {
        var company = await CreateCompanyAsync();
        var platformAdmin = new TestUser(company.AdminId, isPlatformAdmin: true);

        var error = await Assert.ThrowsAsync<ForbiddenException>(
            () => AsAsync(platformAdmin, company.TenantId, s => s.GetAsync(CancellationToken.None)));

        Assert.Equal(TenantSettingsService.TenantAdminRequired, error.Message);
    }

    [Fact]
    public async Task GetAndUpdate_NotSignedIn_SayToSignIn()
    {
        var nobody = new TestUser(null);

        var getError = await Assert.ThrowsAsync<ForbiddenException>(
            () => AsAsync(nobody, Tenant.IthinkaId, s => s.GetAsync(CancellationToken.None)));
        var updateError = await Assert.ThrowsAsync<ForbiddenException>(
            () => AsAsync(nobody, Tenant.IthinkaId, s => s.UpdateAsync(Update(true, true), CancellationToken.None)));

        Assert.Equal("İşlem yapmak için giriş yapılmalıdır.", getError.Message);
        Assert.Equal("İşlem yapmak için giriş yapılmalıdır.", updateError.Message);
    }

    [Fact]
    public async Task GetAndUpdate_CompanyNotFound_IsNotFound()
    {
        var company = await CreateCompanyAsync();
        var lost = new TestUser(company.AdminId, int.MaxValue, isTenantAdmin: true);

        var getError = await Assert.ThrowsAsync<NotFoundException>(
            () => AsAsync(lost, company.TenantId, s => s.GetAsync(CancellationToken.None)));
        var updateError = await Assert.ThrowsAsync<NotFoundException>(
            () => AsAsync(lost, company.TenantId, s => s.UpdateAsync(Update(true, true), CancellationToken.None)));

        Assert.Equal("Şirket bulunamadı.", getError.Message);
        Assert.Equal("Şirket bulunamadı.", updateError.Message);
        Assert.False((await TenantAsync(company.TenantId)).AiEnabled);
    }

    [Theory]
    [InlineData(true, "Veri gönderme izni belirtilmelidir.")]
    [InlineData(false, "Yapay zekâ ayarı belirtilmelidir.")]
    public void UpdateDto_OneSwitchMissing_OnlyThatOneIsAnError(bool sendAiEnabled, string expected)
    {
        // A missing switch is not "off": false is sent for one, the other is left out.
        var dto = sendAiEnabled
            ? new UpdateTenantSettingsDto { AiEnabled = false }
            : new UpdateTenantSettingsDto { AiDataSharingAllowed = false };
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);

        Assert.Equal([expected], results.Select(r => r.ErrorMessage));
    }

    private async Task<AgentStatusDto> StatusAsync(Company company)
    {
        await using var scope = database.CreateScope(company.TenantId);
        var agents = new AgentService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
            new TestUser(company.MemberId, company.TenantId),
            new FakeAgentClient { IsAvailable = true, IsTranscriptionAvailable = true });
        return await agents.GetStatusAsync(CancellationToken.None);
    }

    private async Task<int> TenantAuditLogCountAsync()
    {
        await using var scope = database.CreateScope();
        // tenant: a Tenant change would be logged under no single company, so every company is counted.
        return await scope.ServiceProvider.GetRequiredService<IsdlcDbContext>().AuditLogs
            .IgnoreQueryFilters()
            .CountAsync(a => a.EntityName == nameof(Tenant));
    }
}
