using Isdlc.Application.Common.Models;
using Isdlc.Application.Requirements.Dtos;

namespace Isdlc.Application.Requirements;

/// <summary>
/// The project's requirements. Contract: docs/requirements-documents-api.md
/// Common errors: 404 project or record missing, 409 project not active (writes only).
/// </summary>
public interface IRequirementService
{
    /// <summary>Not cancelled first, then by code.</summary>
    Task<PagedResult<RequirementListItemDto>> ListAsync(
        int projectId, RequirementFilter filter, PageRequest pageRequest, CancellationToken cancellationToken);

    Task<RequirementDto> GetAsync(int projectId, int requirementId, CancellationToken cancellationToken);

    /// <summary>Adds a record as Draft with the next code (GR-001...). 409 if two records got the same code at once.</summary>
    Task<RequirementDto> CreateAsync(int projectId, CreateRequirementDto request, CancellationToken cancellationToken);

    Task<RequirementDto> UpdateAsync(
        int projectId, int requirementId, UpdateRequirementDto request, CancellationToken cancellationToken);

    /// <summary>Any status can follow any other; a cancelled requirement can be made a draft again.</summary>
    Task<RequirementDto> UpdateStatusAsync(
        int projectId, int requirementId, UpdateRequirementStatusDto request, CancellationToken cancellationToken);
}
