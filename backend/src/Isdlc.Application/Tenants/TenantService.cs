using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Common.Models;
using Isdlc.Application.Identity;
using Isdlc.Application.Identity.Dtos;
using Isdlc.Application.Tenants.Dtos;
using Isdlc.Domain.Identity;
using Isdlc.Domain.Tenants;
using Microsoft.EntityFrameworkCore;

namespace Isdlc.Application.Tenants;

public class TenantService(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    ITenantContext tenantContext,
    ITenantTemplates templates,
    IPasswordHasher passwordHasher,
    TimeProvider timeProvider) : ITenantService
{
    public async Task<PagedResult<TenantDto>> ListAsync(PageRequest pageRequest, CancellationToken cancellationToken)
    {
        RequirePlatformAdmin();

        var totalCount = await db.Tenants.CountAsync(cancellationToken);
        var items = await db.Tenants
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .ThenBy(t => t.Id)
            .ApplyPaging(pageRequest)
            .Select(t => new TenantDto(
                t.Id,
                t.Code,
                t.Name,
                t.IsActive,
                // tenant: only how many users the company has (TenantDto.userCount, docs/multi-tenancy.md → Okuma kuralı).
                db.Users.IgnoreQueryFilters().Count(u => u.TenantId == t.Id),
                t.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<TenantDto>(items, totalCount);
    }

    public async Task<TenantDto> CreateAsync(CreateTenantDto request, CancellationToken cancellationToken)
    {
        RequirePlatformAdmin();
        var admin = request.Admin!; // [Required]: the API answers 400 before this runs.
        var role = UserAdminRules.CompanyRole(admin.Role!.Value);
        var code = request.Code.Trim();
        var email = admin.Email.Trim();

        if (!TenantCodeRules.IsValid(code))
        {
            throw new BusinessRuleException("Şirket kodu küçük harf, rakam ve tireden oluşmalıdır (2–50 karakter).");
        }

        if (await db.Tenants.AnyAsync(t => t.Code == code, cancellationToken))
        {
            throw new ConflictException("Bu şirket kodu kullanılıyor.");
        }

        await UserAdminService.RequireUnusedEmailAsync(db, email, cancellationToken);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var tenant = new Tenant { Code = code, Name = request.Name.Trim(), IsActive = true, CreatedAt = now };

        // The company, its template copy and its first administrator are saved together or not at all.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(cancellationToken);

        // Opening a company is the one place a service works as another company (docs/multi-tenancy.md → Seed ve şablon):
        // the template copy and the administrator are written to the new company, then the request goes back to its own.
        var previous = tenantContext.TenantId;
        tenantContext.Use(tenant.Id);
        try
        {
            await templates.ApplyAsync(cancellationToken);

            db.Users.Add(new User
            {
                FullName = admin.FullName.Trim(),
                Email = email,
                Role = role,
                IsTenantAdmin = true,
                MustChangePassword = true,
                PasswordHash = passwordHasher.Hash(admin.TemporaryPassword),
                CreatedAt = now,
            });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            tenantContext.Use(previous);
        }

        return new TenantDto(tenant.Id, tenant.Code, tenant.Name, tenant.IsActive, UserCount: 1, tenant.CreatedAt);
    }

    public async Task<TenantDto> UpdateAsync(int tenantId, UpdateTenantDto request, CancellationToken cancellationToken)
    {
        RequirePlatformAdmin();
        var tenant = await db.Tenants.SingleOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            ?? throw new NotFoundException("Şirket bulunamadı.");

        tenant.Name = request.Name.Trim();
        tenant.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);

        // tenant: only how many users the company has (TenantDto.userCount).
        var userCount = await db.Users.IgnoreQueryFilters().CountAsync(u => u.TenantId == tenantId, cancellationToken);
        return new TenantDto(tenant.Id, tenant.Code, tenant.Name, tenant.IsActive, userCount, tenant.CreatedAt);
    }

    public async Task<TenantSummaryDto> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new NotFoundException("Şirket bulunamadı.");
        return await db.Tenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => new TenantSummaryDto(t.Id, t.Code, t.Name))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Şirket bulunamadı.");
    }

    private void RequirePlatformAdmin()
    {
        if (!currentUser.IsPlatformAdmin)
        {
            throw new ForbiddenException("Bu işlem yalnızca platform yöneticisine açıktır.");
        }
    }
}
