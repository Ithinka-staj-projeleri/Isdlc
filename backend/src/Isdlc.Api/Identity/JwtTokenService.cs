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

    /// <summary>"true" for a Şirket Yöneticisi, "false" otherwise.</summary>
    public const string TenantAdminClaim = "tenantAdmin";

    /// <summary>Only on the token of a user with a temporary password: every request but changing it gets 403.</summary>
    public const string MustChangePasswordClaim = "mustChangePassword";
    public const string PlatformAdminClaim = "platformAdmin";

    private readonly JsonWebTokenHandler _handler = new();

    public TokenResult Create(User user)
    {
        var expiresAt = timeProvider.GetUtcNow().UtcDateTime.AddMinutes(options.ExpiresInMinutes);
        List<Claim> claims =
        [
            new Claim("sub", user.Id.ToString()),
            new Claim("name", user.FullName),
            new Claim("email", user.Email),
            // Permissions are not in the token: a change by the company administrator counts from the next request.
            new Claim(PlatformAdminClaim, user.IsPlatformAdmin ? "true" : "false"),
            new Claim(TenantAdminClaim, user.IsTenantAdmin ? "true" : "false"),
        ];
        if (user.TenantId is { } tenantId)
        {
            claims.Add(new Claim(TenantClaim, tenantId.ToString(CultureInfo.InvariantCulture)));
        }

        if (user.MustChangePassword)
        {
            claims.Add(new Claim(MustChangePasswordClaim, "true"));
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