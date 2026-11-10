using Isdlc.Application.Common.Models;
using Isdlc.Application.Risks.Dtos;

namespace Isdlc.Application.Risks;

/// <summary>
/// The project's risks. Contract: docs/risks-api.md
/// Common errors: 404 project or risk missing, 409 project not active (writes only).
/// </summary>
public interface IRiskService
{
    /// <summary>Not closed first, then by score (highest first), then by code.</summary>
    Task<PagedResult<RiskListItemDto>> ListAsync(
        int projectId, RiskFilter filter, PageRequest pageRequest, CancellationToken cancellationToken);

    /// <summary>Counts of the risks that are not closed, per probability × impact cell and per severity.</summary>
    Task<RiskMatrixDto> GetMatrixAsync(int projectId, CancellationToken cancellationToken);

    Task<RiskDto> GetAsync(int projectId, int riskId, CancellationToken cancellationToken);

    /// <summary>Adds a risk as Open with the next code (RSK-001...). 409 if two risks got the same code at once.</summary>
    Task<RiskDto> CreateAsync(int projectId, CreateRiskDto request, CancellationToken cancellationToken);

    Task<RiskDto> UpdateAsync(int projectId, int riskId, UpdateRiskDto request, CancellationToken cancellationToken);
}
