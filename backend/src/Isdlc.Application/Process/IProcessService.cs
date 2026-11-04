using Isdlc.Application.Process.Dtos;

namespace Isdlc.Application.Process;

/// <summary>
/// ISDLC process flow and the progress of projects through it.
/// Contract: docs/process-api.md
/// </summary>
public interface IProcessService
{
    /// <summary>All 13 stages with their steps, both in flow order.</summary>
    Task<IReadOnlyList<ProcessStageDto>> GetStagesAsync(CancellationToken cancellationToken);
}
