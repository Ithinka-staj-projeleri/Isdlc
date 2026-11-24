using System.Globalization;
using Isdlc.Application.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace Isdlc.Api.Identity;

/// <summary>
/// Runs after sign-in is read, before authorization (docs/multi-tenancy.md → İstek başına, Parola):
/// <list type="bullet">
/// <item>A user who was deactivated, or whose company was, gets 401 at once instead of when the token expires.
/// The answer is cached for a minute, so the database is read at most once a minute per user.</item>
/// <item>A user with a temporary password gets 403 on every request but changing it. Read from the token, not the database.</item>
/// </list>
/// </summary>
public class AccountGateMiddleware(RequestDelegate next, IMemoryCache cache)
{
    public static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(1);

    public const string ChangePasswordPath = "/api/v1/auth/change-password";

    public async Task InvokeAsync(HttpContext context, IAccountStatusService accountStatus)
    {
        var principal = context.User;
        if (principal.Identity?.IsAuthenticated != true
            || !int.TryParse(principal.FindFirst("sub")?.Value, out var userId))
        {
            await next(context);
            return;
        }

        var tenantId = int.TryParse(principal.FindFirst(JwtTokenService.TenantClaim)?.Value, NumberStyles.None,
            CultureInfo.InvariantCulture, out var id) ? id : (int?)null;

        var isActive = await cache.GetOrCreateAsync(CacheKey(userId, tenantId), entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheFor;
            return accountStatus.IsActiveAsync(userId, tenantId, context.RequestAborted);
        });

        if (!isActive)
        {
            // The same answer as a failed sign-in: the screen goes back to the sign-in page.
            await WriteProblemAsync(context, StatusCodes.Status401Unauthorized, "Giriş yapılamadı.", "E-posta veya parola hatalı.");
            return;
        }

        if (principal.FindFirst(JwtTokenService.MustChangePasswordClaim)?.Value == "true"
            && !context.Request.Path.Equals(ChangePasswordPath, StringComparison.OrdinalIgnoreCase))
        {
            await WriteProblemAsync(context, StatusCodes.Status403Forbidden, "Yetkiniz yok", "Devam etmek için parolanızı değiştirin.");
            return;
        }

        await next(context);
    }

    /// <summary>Also used to forget a user's cached answer at once when they or their company are deactivated (T4 · 2).</summary>
    public static string CacheKey(int userId, int? tenantId) => $"account-status:{userId}:{tenantId}";

    private static async Task WriteProblemAsync(HttpContext context, int status, string title, string detail)
    {
        context.Response.StatusCode = status;
        var problems = context.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problems.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = detail },
        });
    }
}
