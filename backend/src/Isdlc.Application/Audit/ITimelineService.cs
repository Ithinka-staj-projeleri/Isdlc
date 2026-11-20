using Isdlc.Application.Audit.Dtos;
using Isdlc.Application.Common.Models;

namespace Isdlc.Application.Audit;

/// <summary>
/// What happened on a project, newest first: process steps, minutes, decisions, approved requirements, closed risks.
/// Read only. Contract: docs/audit-api.md → Zaman çizelgesi.
/// </summary>
public interface ITimelineService
{
    /// <summary>404 when the project does not exist.</summary>
    Task<PagedResult<TimelineEventDto>> ListProjectTimelineAsync(
        int projectId, PageRequest pageRequest, CancellationToken cancellationToken);
}
