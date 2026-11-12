using Isdlc.Application.Common.Models;
using Isdlc.Application.ProcessImprovements.Dtos;

namespace Isdlc.Application.ProcessImprovements;

/// <summary>
/// Process improvement proposals (Faz 4 · E5). A proposal is a Project on the process-improvement flow; its steps run
/// through the Process module. Read by anyone signed in, written only while active (409). Contract: docs/process-improvement-api.md
/// </summary>
public interface IProcessImprovementService
{
    Task<PagedResult<ImprovementListItemDto>> ListAsync(
        ImprovementFilter filter, PageRequest pageRequest, CancellationToken cancellationToken);

    Task<ImprovementDto> GetAsync(int id, CancellationToken cancellationToken);

    Task<ImprovementDto> CreateAsync(CreateImprovementDto request, CancellationToken cancellationToken);

    Task<ImprovementDto> UpdateAsync(int id, UpdateImprovementDto request, CancellationToken cancellationToken);

    Task<ImprovementDto> SavePrioritizationAsync(int id, SavePrioritizationDto request, CancellationToken cancellationToken);

    Task<ImprovementDto> SaveStudyAsync(int id, SaveStudyDto request, CancellationToken cancellationToken);

    Task<ImprovementDto> SavePilotEvaluationAsync(int id, SavePilotEvaluationDto request, CancellationToken cancellationToken);
}
