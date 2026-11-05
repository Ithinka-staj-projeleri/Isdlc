using Isdlc.Api.Common;
using Isdlc.Application.Identity;
using Isdlc.Application.Identity.Dtos;
using Isdlc.Domain.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Isdlc.Api.Controllers;

/// <summary>/api/v1/users — Identity module. For now: the people list used by forms.</summary>
public class UsersController(IUserService userService) : ApiControllerBase
{
    /// <summary>Active users sorted by name: ?role=BusinessAnalyst for one role, nothing for everyone.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<UserLookupDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<UserLookupDto>> List([FromQuery] UserRole? role, CancellationToken cancellationToken)
    {
        return await userService.ListActiveAsync(role, cancellationToken);
    }
}
