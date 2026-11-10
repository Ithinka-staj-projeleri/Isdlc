using Isdlc.Application.Audit.Dtos;
using Isdlc.Application.Common.Models;

namespace Isdlc.Application.Audit;

/// <summary>
/// Reads the audit log of a project (written automatically by AuditLogInterceptor; never changed here).
/// Contract: docs/audit-api.md
/// </summary>
public interface IAuditService
{
    /// <summary>Newest first. 404 when the project does not exist.</summary>
    Task<PagedResult<AuditLogDto>> ListProjectLogAsync(
        int projectId, AuditLogFilter filter, PageRequest pageRequest, CancellationToken cancellationToken);
}
