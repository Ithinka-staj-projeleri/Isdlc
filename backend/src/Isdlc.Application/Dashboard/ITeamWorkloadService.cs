using Isdlc.Application.Dashboard.Dtos;

namespace Isdlc.Application.Dashboard;

/// <summary>
/// Ekip iş yükü (Faz 5 · F2): open issues and waiting steps per active user, on active records of every flow.
/// The dashboard shows it; the person profile and the owner suggestion (F9) use the same numbers.
/// Contract: docs/dashboard-api.md → "Ekip iş yükü".
/// </summary>
public interface ITeamWorkloadService
{
    /// <summary>Every active user, the busiest first.</summary>
    Task<IReadOnlyList<TeamMemberWorkloadDto>> GetAsync(CancellationToken cancellationToken);
}
