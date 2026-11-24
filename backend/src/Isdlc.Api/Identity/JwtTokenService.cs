using System.Globalization;
using System.Security.Claims;
using System.Text;
using Isdlc.Application.Identity;
using Isdlc.Domain.Identity;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Isdlc.Api.Identity;

public class JwtTokenService(JwtOptions options, TimeProvider timeProvider) : ITokenService
{
    /// <summary>The user's company (Tenant.Id); missing only for the Platform Administrator (docs/multi-tenancy.md → Kimlik).</summary>
    public const string TenantClaim = "tenant";

    private readonly JsonWebTokenHandler _handler = new();

    public TokenResult Create(User user)
    {
        var expiresAt = timeProvider.GetUtcNow().UtcDateTime.AddMinutes(options.ExpiresInMinutes);
        List<Claim> claims =
        [
            new Claim("sub", user.Id.ToString()),
            new Claim("name", user.FullName),
            new Claim("email", user.Email),
            new Claim("role", user.Role.ToString()),
        ];
        if (user.TenantId is { } tenantId)
        {
            claims.Add(new Claim(TenantClaim, tenantId.ToString(CultureInfo.InvariantCulture)));
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
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