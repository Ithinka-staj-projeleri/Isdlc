using Isdlc.Api.Common;
using Isdlc.Application.Identity;
using Isdlc.Application.Identity.Dtos;
using Isdlc.Domain.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Isdlc.Api.Controllers;

/// <summary>/api/v1/users — Identity module: the people list used by forms, and the person profile (docs/users-api.md).</summary>
public class UsersController(IUserService userService, IUserProfileService profileService) : ApiControllerBase
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
}
