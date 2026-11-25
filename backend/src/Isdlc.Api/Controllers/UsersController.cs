using Isdlc.Api.Common;
using Isdlc.Application.Identity;
using Isdlc.Application.Identity.Dtos;
using Isdlc.Domain.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Isdlc.Api.Controllers;

/// <summary>
/// /api/v1/users — Identity module: the people list used by forms, the person profile (docs/users-api.md), and the
/// company administrator's user management (docs/multi-tenancy.md → Şirket Yöneticisi).
/// </summary>
public class UsersController(IUserService userService, IUserProfileService profileService, IUserAdminService adminService)
    : ApiControllerBase
{
    /// <summary>Active users sorted by name: ?role=BusinessAnalyst for one role, nothing for everyone.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<UserLookupDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<UserLookupDto>> List([FromQuery] UserRole? role, CancellationToken cancellationToken)
    {
        return await userService.ListActiveAsync(role, cancellationToken);
    }

    /// <summary>Active users with their expertise and workload, sorted by name: the open issue form's owner selector.</summary>
    [HttpGet("workload")]
    [ProducesResponseType<IReadOnlyList<UserWorkloadDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<UserWorkloadDto>> Workload(CancellationToken cancellationToken)
    {
        return await profileService.ListWorkloadAsync(cancellationToken);
    }

    /// <summary>A person's profile: expertise, note, workload, open issues and waiting steps.</summary>
    [HttpGet("{id:int}/profile")]
    [ProducesResponseType<UserProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<UserProfileDto> Profile(int id, CancellationToken cancellationToken)
    {
        return await profileService.GetAsync(id, cancellationToken);
    }

    /// <summary>Saves the signed-in user's own expertise and "how to work with me" note.</summary>
    [HttpPut("me/profile")]
    [ProducesResponseType<UserProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<UserProfileDto> UpdateMyProfile(UpdateMyProfileDto dto, CancellationToken cancellationToken)
    {
        return await profileService.UpdateMineAsync(dto, cancellationToken);
    }

    // ---- Company administrator (Faz 6 · T4): 403 for anyone else ----

    /// <summary>Every user of the company, inactive ones too, with e-mail and marks; the users screen.</summary>
    [HttpGet("manage")]
    [ProducesResponseType<IReadOnlyList<UserDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IReadOnlyList<UserDto>> Manage(CancellationToken cancellationToken)
    {
        return await adminService.ListAsync(cancellationToken);
    }

    /// <summary>A new user of the company with a temporary password, changed at the first sign-in.</summary>
    [HttpPost]
    [ProducesResponseType<UserDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserDto>> Create(CreateUserDto request, CancellationToken cancellationToken)
    {
        var user = await adminService.CreateAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, user);
    }

    /// <summary>Name, role, active and the administrator mark; the e-mail never changes.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<UserDto> Update(int id, UpdateUserDto request, CancellationToken cancellationToken)
    {
        return await adminService.UpdateAsync(id, request, cancellationToken);
    }

    /// <summary>Gives a temporary password; the user must change it at the next sign-in.</summary>
    [HttpPost("{id:int}/reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetPassword(int id, ResetPasswordDto request, CancellationToken cancellationToken)
    {
        await adminService.ResetPasswordAsync(id, request, cancellationToken);
        return NoContent();
    }
}
