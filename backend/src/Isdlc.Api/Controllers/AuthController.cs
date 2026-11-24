using Isdlc.Api.Common;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Identity;
using Isdlc.Application.Identity.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Isdlc.Api.Controllers;

/// <summary>Sign-in: /api/v1/auth</summary>
public class AuthController(IAuthService authService, ICurrentUserService currentUser) : ApiControllerBase
{
    /// <summary>Returns a JWT. A wrong e-mail, wrong password or an inactive user all give the same 401.</summary>
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType<LoginResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResultDto>> Login(LoginDto request, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request, cancellationToken);
        if (result is null)
        {
            return Problem(
                title: "Giriş yapılamadı.",
                detail: "E-posta veya parola hatalı.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        return Ok(result);
    }

    /// <summary>
    /// Changes the signed-in user's password; the only request a user with a temporary password may make.
    /// Returns a new token without "mustChangePassword". Contract: docs/multi-tenancy.md → Parola.
    /// </summary>
    [HttpPost("change-password")]
    [ProducesResponseType<LoginResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<LoginResultDto> ChangePassword(ChangePasswordDto request, CancellationToken cancellationToken)
    {
        // The fallback policy lets only signed-in users here, so the token always has a user id.
        return await authService.ChangePasswordAsync(currentUser.UserId!.Value, request, cancellationToken);
    }
}
