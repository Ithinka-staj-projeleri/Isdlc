using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Identity.Dtos;
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
            .SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

        // Unknown e-mail, inactive user and wrong password all give the same answer.
        if (user is null || !user.IsActive || !passwordHasher.Verify(user.PasswordHash, request.Password))
        {
            return null;
        }

        // The user of an inactive company gets the same answer; that the company exists is not told.
        if (user.TenantId is { } tenantId
            && !await db.Tenants.AnyAsync(t => t.Id == tenantId && t.IsActive, cancellationToken))
        {
            return null;
        }

        var token = tokenService.Create(user);
        return new LoginResultDto(token.Value, token.ExpiresAt,
            new AuthUserDto(user.Id, user.FullName, user.Email, user.Role));
    }
}