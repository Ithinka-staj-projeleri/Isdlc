using Isdlc.Application.Dashboard.Dtos;

namespace Isdlc.Application.Dashboard;

/// <summary>
/// The dashboard: what waits for the signed-in user and how the projects stand. Read only; no tables of its own.
/// Contract: docs/dashboard-api.md
/// </summary>
public interface IDashboardService
{
    /// <summary>
    /// Every section in one call. scope narrows the general sections to the user's projects;
    /// the "my ..." sections are always personal. 403 when nobody is signed in.
    /// </summary>
    Task<DashboardDto> GetAsync(DashboardScope scope, CancellationToken cancellationToken);
}
