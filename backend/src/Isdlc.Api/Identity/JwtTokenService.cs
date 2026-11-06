using System.Security.Claims;
using System.Text;
using Isdlc.Application.Identity;
using Isdlc.Domain.Identity;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Isdlc.Api.Identity;

public class JwtTokenService(JwtOptions options, TimeProvider timeProvider) : ITokenService
{
    private readonly JsonWebTokenHandler _handler = new();

    public TokenResult Create(User user)
    {
        var expiresAt = timeProvider.GetUtcNow().UtcDateTime.AddMinutes(options.ExpiresInMinutes);
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim("sub", user.Id.ToString()),
                new Claim("name", user.FullName),
                new Claim("email", user.Email),
                new Claim("role", user.Role.ToString()),
            ]),
            Issuer = options.Issuer,
            Audience = options.Audience,
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
                SecurityAlgorithms.HmacSha256),
        };

        return new TokenResult(_handler.CreateToken(descriptor), expiresAt);
    }
}