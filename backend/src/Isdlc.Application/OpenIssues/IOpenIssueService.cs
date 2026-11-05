using Isdlc.Application.Common.Models;
using Isdlc.Application.OpenIssues.Dtos;

namespace Isdlc.Application.OpenIssues;

/// <summary>
/// The project's Open Issues List. Contract: docs/open-issues-meetings-api.md
/// Common errors: 404 project or record missing, 409 project not active (writes only).
/// </summary>
public interface IOpenIssueService
{
    /// <summary>Not closed first, then by due date (none last), then by code.</summary>
    Task<PagedResult<OpenIssueListItemDto>> ListAsync(
        int projectId, OpenIssueFilter filter, PageRequest pageRequest, CancellationToken cancellationToken);

    Task<OpenIssueDto> GetAsync(int projectId, int openIssueId, CancellationToken cancellationToken);

    /// <summary>Adds a record as Open with the next code (AK-001...). 409 if two records got the same code at once.</summary>
    Task<OpenIssueDto> CreateAsync(int projectId, CreateOpenIssueDto request, CancellationToken cancellationToken);

    Task<OpenIssueDto> UpdateAsync(
        int projectId, int openIssueId, UpdateOpenIssueDto request, CancellationToken cancellationToken);

    Task<OpenIssueDto> UpdateStatusAsync(
        int projectId, int openIssueId, UpdateOpenIssueStatusDto request, CancellationToken cancellationToken);
}
