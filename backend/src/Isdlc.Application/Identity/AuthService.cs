using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Identity.Dtos;
using Isdlc.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Isdlc.Application.Identity;

public class AuthService(IApplicationDbContext db, IPasswordHasher passwordHasher, ITokenService tokenService)
    : IAuthService
{
    public async Task<LoginResultDto?> LoginAsync(LoginDto request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim();
        // tenant: the company is not known before sign-in; the e-mail is unique across companies and gives it.
        var user = await db.Users.IgnoreQueryFilters().AsNoTracking()
            .Include(u => u.Permissions)
            .SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

        // Unknown e-mail, inactive user and wrong password all give the same answer.
        if (user is null || !user.IsActive || !passwordHasher.Verify(user.PasswordHash, request.Password))
        {
            return null;
        }

        // The user of an inactive company gets the same answer; that the company exists is not told.
        var tenant = await TenantOfAsync(user, cancellationToken);
        if (user.TenantId is not null && tenant is null)
        {
            return null;
        }

        return Result(user, tenant);
    }

    public async Task<LoginResultDto> ChangePasswordAsync(
        int userId, ChangePasswordDto request, CancellationToken cancellationToken = default)
    {
        // tenant: the signed-in user's own row, found by the id in their token. The Platform Administrator has no
        // company, so the filter would never find them (docs/multi-tenancy.md → Okuma kuralı).
        var user = await db.Users.IgnoreQueryFilters()
            .Include(u => u.Permissions)
            .SingleOrDefaultAsync(u => u.Id == userId && u.IsActive, cancellationToken)
            ?? throw new ForbiddenException("İşlem yapmak için giriş yapılmalıdır.");

        if (!passwordHasher.Verify(user.PasswordHash, request.CurrentPassword))
        {
            throw new BusinessRuleException("Mevcut parola hatalı.");
        }

        if (passwordHasher.Verify(user.PasswordHash, request.NewPassword))
        {
            throw new BusinessRuleException("Yeni parola eskisinden farklı olmalıdır.");
        }

        user.PasswordHash = passwordHasher.Hash(request.NewPassword);
        user.MustChangePassword = false;
        await db.SaveChangesAsync(cancellationToken);

        // A new token without the "mustChangePassword" claim, so the user can go on at once.
        return Result(user, await TenantOfAsync(user, cancellationToken));
    }

    public async Task<AuthUserDto> GetMeAsync(int userId, CancellationToken cancellationToken = default)
    {
        // tenant: the signed-in user's own row, found by the id in their token; the Platform Administrator has no
        // company, so the filter would never find them (docs/configurability.md → Giriş ve istek).
        var user = await db.Users.IgnoreQueryFilters().AsNoTracking()
            .Include(u => u.Permissions)
            .SingleOrDefaultAsync(u => u.Id == userId && u.IsActive, cancellationToken)
            ?? throw new ForbiddenException("İşlem yapmak için giriş yapılmalıdır.");

        return ToAuthUser(user, await TenantOfAsync(user, cancellationToken));
    }

    /// <summary>The user's company when it is active; null for the Platform Administrator and an inactive company.</summary>
    private async Task<TenantSummaryDto?> TenantOfAsync(User user, CancellationToken cancellationToken) =>
        user.TenantId is not { } tenantId
            ? null
            : await db.Tenants
                .AsNoTracking()
                .Where(t => t.Id == tenantId && t.IsActive)
                .Select(t => new TenantSummaryDto(t.Id, t.Code, t.Name))
                .SingleOrDefaultAsync(cancellationToken);

    private LoginResultDto Result(User user, TenantSummaryDto? tenant)
    {
        var token = tokenService.Create(user);
        return new LoginResultDto(
            token.Value,
            token.ExpiresAt,
            ToAuthUser(user, tenant),
            user.MustChangePassword);
    }

    private static AuthUserDto ToAuthUser(User user, TenantSummaryDto? tenant) =>
        new(user.Id, user.FullName, user.Email, tenant, user.IsTenantAdmin, user.IsPlatformAdmin,
            user.Permissions.Select(p => p.Permission).Order().ToList());
}
