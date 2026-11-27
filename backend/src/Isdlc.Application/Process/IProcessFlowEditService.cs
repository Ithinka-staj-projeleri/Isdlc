using Isdlc.Application.Process.Dtos;

namespace Isdlc.Application.Process;

/// <summary>
/// The company administrator edits the company's own copy of a flow (Faz 7 · A7): stages, steps, who does them, what
/// they need and when they are skipped, their order. Every method answers 403 for anyone else, 404 for a flow, stage or
/// step of no such company record, 409 when the flow changed meanwhile, and returns the flow with its inactive parts.
/// Contract: docs/configurability.md → Akış düzenleme.
/// </summary>
public interface IProcessFlowEditService
{
    Task<ProcessFlowDto> UpdateFlowAsync(string code, UpdateFlowDto request, CancellationToken cancellationToken);

    Task<ProcessFlowDto> CopyFlowAsync(string code, CopyFlowDto request, CancellationToken cancellationToken);

    Task<ProcessFlowDto> AddStageAsync(string code, SaveStageDto request, CancellationToken cancellationToken);

    Task<ProcessFlowDto> UpdateStageAsync(int stageId, SaveStageDto request, CancellationToken cancellationToken);

    Task<ProcessFlowDto> SetStageActiveAsync(int stageId, bool active, FlowVersionDto request, CancellationToken cancellationToken);

    Task<ProcessFlowDto> AddStepAsync(string code, AddStepDto request, CancellationToken cancellationToken);

    Task<ProcessFlowDto> UpdateStepAsync(int stepId, SaveStepDto request, CancellationToken cancellationToken);

    Task<ProcessFlowDto> SetStepActiveAsync(int stepId, bool active, FlowVersionDto request, CancellationToken cancellationToken);

    Task<ProcessFlowDto> ReorderAsync(string code, ReorderFlowDto request, CancellationToken cancellationToken);
}
