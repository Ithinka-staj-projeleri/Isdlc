using Isdlc.Application.Common.Interfaces;

namespace Isdlc.Api.Identity;

/// <summary>
/// Reads the signed-in user from the JWT: the Id from "sub", the company from "tenant". Null when nobody is signed in.
/// A token from before Phase 6 has no "tenant": its user sees no company record until signing in again.
/// </summary>
public class HttpCurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public int? UserId => ReadInt("sub");

    public int? TenantId => ReadInt(JwtTokenService.TenantClaim);

    private int? ReadInt(string claim) =>
        int.TryParse(httpContextAccessor.HttpContext?.User.FindFirst(claim)?.Value, out var value) ? value : null;
}
