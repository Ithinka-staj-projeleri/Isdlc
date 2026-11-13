using Isdlc.Application.Common.Models;
using Isdlc.Application.Process.Dtos;

namespace Isdlc.Application.Process;

/// <summary>
/// The company's work flows and the progress of records through them.
/// Contract: docs/process-api.md
/// </summary>
public interface IProcessService
{
    /// <summary>All flows without their steps, in their display order.</summary>
    Task<IReadOnlyList<ProcessFlowListItemDto>> GetFlowsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// A flow with its stages and steps, both in flow order.
    /// Throws NotFoundException when no flow has this code.
    /// </summary>
    Task<ProcessFlowDto> GetFlowAsync(string code, CancellationToken cancellationToken);

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

    /// <summary>
    /// Completes the current non-decision step and moves the project to the next step
    /// (or closes it after the last step). Returns the updated process state.
    /// Errors: 404 project missing, 409 not active / step changed / concurrent update,
    /// 403 role cannot act, 400 the step is a decision step.
    /// </summary>
    Task<ProjectProcessDto> CompleteStepAsync(int projectId, CompleteStepDto request, CancellationToken cancellationToken);

    /// <summary>
    /// Answers the current decision step. "Yes" moves to the next step; "No" closes the project,
    /// goes back to the chosen step, or stays on the step (docs/data-model.md → Süreç ilerleme kuralları).
    /// Errors: as CompleteStepAsync, plus 400 for a non-decision step or an invalid return step.
    /// </summary>
    Task<ProjectProcessDto> AnswerStepAsync(int projectId, AnswerStepDto request, CancellationToken cancellationToken);

    /// <summary>
    /// The Software Department Manager rejects the request on any step: the project becomes Rejected
    /// and a Rejected record with the reason is written on the current step.
    /// Errors: 404 project missing, 409 not active / step changed / concurrent update,
    /// 403 not the manager, 400 missing reason.
    /// </summary>
    Task<ProjectProcessDto> RejectAsync(int projectId, RejectProjectDto request, CancellationToken cancellationToken);

    /// <summary>
    /// Puts a request on hold with the Talep Ret / Erteleme Formu (Software Department Manager only; 400 after the
    /// request stages). It keeps its current step; nothing can be done on it until it is taken back.
    /// </summary>
    Task<ProjectProcessDto> PostponeAsync(int projectId, PostponeProjectDto request, CancellationToken cancellationToken);

    /// <summary>Takes a postponed request back (Software Department Manager only): it is active again on its step.</summary>
    Task<ProjectProcessDto> ResumeAsync(int projectId, ResumeProjectDto request, CancellationToken cancellationToken);

    /// <summary>The project's filled-in Talep Ret / Erteleme Formları, newest first. 404 when the project does not exist.</summary>
    Task<IReadOnlyList<RequestDecisionDto>> GetRequestDecisionsAsync(int projectId, CancellationToken cancellationToken);
}
