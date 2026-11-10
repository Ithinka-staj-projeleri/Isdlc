using Isdlc.Api.Common;
using Isdlc.Application.Audit;
using Isdlc.Application.Audit.Dtos;
using Isdlc.Application.Common.Models;
using Microsoft.AspNetCore.Mvc;

namespace Isdlc.Api.Controllers;

/// <summary>
/// The project's change history: /api/v1/projects/{projectId}/audit-logs. Read only; the log is written automatically.
/// It is a sub-resource of projects, so the route is written in full ("/" ignores the base route).
/// </summary>
public class AuditLogsController(IAuditService auditService) : ApiControllerBase
{
    /// <summary>Newest first. Filter: ?entityType=OpenIssue. Paging: ?page=1&amp;pageSize=20</summary>
    [HttpGet("/api/v1/projects/{projectId:int}/audit-logs")]
    [ProducesResponseType<PagedResult<AuditLogDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<PagedResult<AuditLogDto>> List(
        int projectId, [FromQuery] AuditLogFilter filter, [FromQuery] PageRequest pageRequest,
        CancellationToken cancellationToken)
    {
        return await auditService.ListProjectLogAsync(projectId, filter, pageRequest, cancellationToken);
    }
}
