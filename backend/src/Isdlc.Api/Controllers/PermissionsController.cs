using Isdlc.Api.Common;
using Isdlc.Application.Identity.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Isdlc.Api.Controllers;

/// <summary>/api/v1/permissions — the fixed permission list and ready-made sets (Faz 7 · A2); every signed-in user.</summary>
public class PermissionsController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<PermissionCatalogDto>(StatusCodes.Status200OK)]
    public PermissionCatalogDto List() => PermissionCatalogDto.Build();
}
