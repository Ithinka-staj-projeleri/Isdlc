using Isdlc.Api.Common;
using Isdlc.Application.Identity;
using Isdlc.Application.Identity.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Isdlc.Api.Controllers;

/// <summary>Sign-in: /api/v1/auth</summary>
public class AuthController(IAuthService authService) : ApiControllerBase
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
}