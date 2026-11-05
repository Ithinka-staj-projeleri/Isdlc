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
        var user = await db.Users.AsNoTracking()
            .SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

        // Unknown e-mail, inactive user and wrong password all give the same answer.
        if (user is null || !user.IsActive || !passwordHasher.Verify(user.PasswordHash, request.Password))
        {
            return null;
        }

        var token = tokenService.Create(user);
        return new LoginResultDto(token.Value, token.ExpiresAt,
            new AuthUserDto(user.Id, user.FullName, user.Email, user.Role));
    }
}