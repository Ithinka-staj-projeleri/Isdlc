using Isdlc.Application.Common.Models;
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

    /// <summary>
    /// The project's place in the flow and what the signed-in user can do on the current step.
    /// Throws NotFoundException when the project does not exist.
    /// </summary>
    Task<ProjectProcessDto> GetProjectProcessAsync(int projectId, CancellationToken cancellationToken);

    /// <summary>
    /// The project's process history, newest first.
    /// Throws NotFoundException when the project does not exist.
    /// </summary>
    Task<PagedResult<StepRecordDto>> GetStepRecordsAsync(
        int projectId, PageRequest pageRequest, CancellationToken cancellationToken);
}
