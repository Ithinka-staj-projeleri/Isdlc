using Isdlc.Api.Common;
using Isdlc.Application.Dashboard;
using Isdlc.Application.Dashboard.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Isdlc.Api.Controllers;

/// <summary>The dashboard: /api/v1/dashboard</summary>
public class DashboardController(IDashboardService dashboardService) : ApiControllerBase
{
    /// <summary>
    /// Every section in one call. ?scope=mine counts only the user's projects (analyst or project manager)
    /// in the general sections. Not paged: each list returns its first 10 rows and a total.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<DashboardDto>(StatusCodes.Status200OK)]
    public async Task<DashboardDto> Get([FromQuery] DashboardScope scope, CancellationToken cancellationToken)
    {
        return await dashboardService.GetAsync(scope, cancellationToken);
    }
}
