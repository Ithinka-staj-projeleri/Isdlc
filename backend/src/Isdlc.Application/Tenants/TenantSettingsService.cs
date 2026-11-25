using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Tenants.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Isdlc.Application.Tenants;

public class TenantSettingsService(IApplicationDbContext dbContext, ICurrentUserService currentUser) : ITenantSettingsService
{
    public const string TenantAdminRequired = "Bu işlem için şirket yöneticisi olmalısınız.";

    public async Task<TenantSettingsDto> GetAsync(CancellationToken cancellationToken)
    {
        var tenantId = EnsureTenantAdmin();

        // Tenants has no query filter; the company is always the signed-in administrator's own.
        return await dbContext.Tenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => new TenantSettingsDto(t.AiEnabled, t.AiDataSharingAllowed))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Şirket bulunamadı.");
    }

    public async Task<TenantSettingsDto> UpdateAsync(UpdateTenantSettingsDto dto, CancellationToken cancellationToken)
    {
        var tenantId = EnsureTenantAdmin();

        var tenant = await dbContext.Tenants.SingleOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            ?? throw new NotFoundException("Şirket bulunamadı.");

        // Both are validated as required; the switches stay independent (data sharing alone still keeps every assistant off).
        tenant.AiEnabled = dto.AiEnabled!.Value;
        tenant.AiDataSharingAllowed = dto.AiDataSharingAllowed!.Value;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new TenantSettingsDto(tenant.AiEnabled, tenant.AiDataSharingAllowed);
    }

    /// <summary>The administrator's company id. A Platform Administrator has no company, so is refused like any non-admin.</summary>
    private int EnsureTenantAdmin()
    {
        if (currentUser.UserId is null)
        {
            throw new ForbiddenException("İşlem yapmak için giriş yapılmalıdır.");
        }

        if (!currentUser.IsTenantAdmin || currentUser.TenantId is not { } tenantId)
        {
            throw new ForbiddenException(TenantAdminRequired);
        }

        return tenantId;
    }
}
