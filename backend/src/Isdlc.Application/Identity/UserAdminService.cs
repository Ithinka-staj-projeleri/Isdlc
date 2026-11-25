using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Identity.Dtos;
using Isdlc.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Isdlc.Application.Identity;

public class UserAdminService(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IPasswordHasher passwordHasher,
    IAccountStatusCache accountStatus,
    TimeProvider timeProvider) : IUserAdminService
{
    public async Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken cancellationToken)
    {
        RequireTenantAdmin();

        // The query filter keeps it to the administrator's own company.
        return await db.Users
            .AsNoTracking()
            .OrderBy(u => u.FullName)
            .Select(u => new UserDto(u.Id, u.FullName, u.Email, u.Role, u.IsActive, u.IsTenantAdmin, u.MustChangePassword))
            .ToListAsync(cancellationToken);
    }

    public async Task<UserDto> CreateAsync(CreateUserDto request, CancellationToken cancellationToken)
    {
        RequireTenantAdmin();
        var role = UserAdminRules.CompanyRole(request.Role!.Value); // [Required]: the API answers 400 before this runs.
        var email = request.Email.Trim();
        await RequireUnusedEmailAsync(db, email, cancellationToken);

        // TenantId is left empty: TenantInterceptor writes the administrator's company.
        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            Role = role,
            IsTenantAdmin = request.IsTenantAdmin,
            MustChangePassword = true,
            PasswordHash = passwordHasher.Hash(request.TemporaryPassword),
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(user);
    }

    public async Task<UserDto> UpdateAsync(int userId, UpdateUserDto request, CancellationToken cancellationToken)
    {
        RequireTenantAdmin();
        var user = await FindAsync(userId, cancellationToken);
        var role = UserAdminRules.CompanyRole(request.Role!.Value);

        if (userId == currentUser.UserId && !request.IsActive)
        {
            throw new BusinessRuleException("Kendi hesabınızı pasife alamazsınız.");
        }

        var stopsBeingAdmin = user is { IsActive: true, IsTenantAdmin: true } && (!request.IsActive || !request.IsTenantAdmin);
        if (stopsBeingAdmin
            && !await db.Users.AnyAsync(u => u.Id != userId && u.IsActive && u.IsTenantAdmin, cancellationToken))
        {
            throw new BusinessRuleException("Şirketin en az bir aktif yöneticisi olmalıdır.");
        }

        var deactivated = user.IsActive && !request.IsActive;
        user.FullName = request.FullName.Trim();
        user.Role = role;
        user.IsActive = request.IsActive;
        user.IsTenantAdmin = request.IsTenantAdmin;
        await db.SaveChangesAsync(cancellationToken);

        if (deactivated)
        {
            // Signed out on the next request instead of within a minute.
            accountStatus.Forget(user.Id, user.TenantId);
        }

        return ToDto(user);
    }

    public async Task ResetPasswordAsync(int userId, ResetPasswordDto request, CancellationToken cancellationToken)
    {
        RequireTenantAdmin();
        var user = await FindAsync(userId, cancellationToken);

        user.PasswordHash = passwordHasher.Hash(request.TemporaryPassword);
        user.MustChangePassword = true;
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// 409 when the e-mail is any user's, in any company: it is the sign-in name. Also used when a company is opened.
    /// </summary>
    internal static async Task RequireUnusedEmailAsync(IApplicationDbContext db, string email, CancellationToken cancellationToken)
    {
        // tenant: e-mails are unique across the whole system (docs/multi-tenancy.md → Okuma kuralı).
        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == email, cancellationToken))
        {
            throw new ConflictException("Bu e-posta ile bir kullanıcı zaten var.");
        }
    }

    private void RequireTenantAdmin()
    {
        if (!currentUser.IsTenantAdmin || currentUser.TenantId is null)
        {
            throw new ForbiddenException("Bu işlem için şirket yöneticisi olmalısınız.");
        }
    }

    /// <summary>The query filter hides other companies' users, so one of them is simply not found.</summary>
    private async Task<User> FindAsync(int userId, CancellationToken cancellationToken) =>
        await db.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken)
        ?? throw new NotFoundException("Kullanıcı bulunamadı.");

    private static UserDto ToDto(User u) =>
        new(u.Id, u.FullName, u.Email, u.Role, u.IsActive, u.IsTenantAdmin, u.MustChangePassword);
}

/// <summary>Rules shared by the company's user management and opening a company. Pure: no database.</summary>
public static class UserAdminRules
{
    /// <summary>A role a company may give. The Platform Administrator is never given from a form, only from configuration.</summary>
    public static UserRole CompanyRole(UserRole role) =>
        role == UserRole.PlatformAdmin
            ? throw new BusinessRuleException("Platform yöneticisi rolü verilemez.")
            : role;
}
