using Isdlc.Application.Common.Interfaces;

namespace Isdlc.Api.Identity;

/// <summary>Reads the signed-in user's Id from the "sub" claim of the JWT. Null when nobody is signed in.</summary>
public class HttpCurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public int? UserId =>
        int.TryParse(httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value, out var id) ? id : null;
}