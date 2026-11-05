using Isdlc.Application.Common;
using Isdlc.Application.Process.Dtos;
using Isdlc.Domain.Identity;
using Isdlc.Domain.Projects;

namespace Isdlc.Application.Process;

/// <summary>The current step's fields that decide who can act on it.</summary>
public record StepPermissionInfo(bool IsDecision, UserRole ResponsibleRole, UserRole? ApproverRole);

/// <summary>
/// Pure calculations for a project's place in the flow. No database access, so they can be unit tested directly.
/// "Reference step": the current step for an active project, the step of the last StepRecord otherwise.
/// Rules: docs/process-api.md → "Hesaplanan alanlar" and "Yetki kuralları".
/// </summary>
public static class ProcessProgress
{
    public static int CalculateProgressPercent(ProjectStatus status, int? referenceStepOrder, int totalSteps)
    {
        if (status == ProjectStatus.Closed)
        {
            return 100;
        }

        if (referenceStepOrder is null || totalSteps <= 0)
        {
            return 0;
        }

        // Steps before the reference step are done; integer division rounds down.
        return (referenceStepOrder.Value - 1) * 100 / totalSteps;
    }

    public static ProcessStageState GetStageState(ProjectStatus status, int stageOrder, int? referenceStageOrder)
    {
        if (status == ProjectStatus.Closed)
        {
            return ProcessStageState.Completed;
        }

        if (referenceStageOrder is null)
        {
            return ProcessStageState.Pending;
        }

        if (stageOrder < referenceStageOrder)
        {
            return ProcessStageState.Completed;
        }

        if (stageOrder > referenceStageOrder)
        {
            return ProcessStageState.Pending;
        }

        return status == ProjectStatus.Active ? ProcessStageState.Current : ProcessStageState.Stopped;
    }

    public static AvailableActionsDto GetAvailableActions(
        ProjectStatus status, StepPermissionInfo? currentStep, UserRole? userRole)
    {
        if (status != ProjectStatus.Active)
        {
            return Blocked("Proje aktif değil; süreçte işlem yapılamaz.");
        }

        if (currentStep is null)
        {
            return Blocked("Projenin mevcut adımı bulunamadı.");
        }

        if (userRole is null)
        {
            return Blocked("İşlem yapmak için giriş yapılmalıdır.");
        }

        var canReject = CanReject(userRole.Value);

        if (!CanActOnStep(currentStep, userRole.Value))
        {
            return new AvailableActionsDto(false, false, canReject, GetBlockedReason(currentStep));
        }

        return new AvailableActionsDto(
            CanComplete: !currentStep.IsDecision,
            CanAnswer: currentStep.IsDecision,
            CanReject: canReject,
            BlockedReason: null);
    }

    /// <summary>
    /// Who can complete or answer a step. Used both for the buttons (GetAvailableActions)
    /// and for the check in the commands, so the two can never disagree.
    /// An approval step can only be closed by the approver; any other step by its responsible role.
    /// </summary>
    public static bool CanActOnStep(StepPermissionInfo step, UserRole userRole) =>
        userRole == (step.ApproverRole ?? step.ResponsibleRole);

    /// <summary>Only the Software Department Manager can reject a request.</summary>
    public static bool CanReject(UserRole userRole) => userRole == UserRole.SoftwareDepartmentManager;

    /// <summary>Message for a user who cannot act on the step.</summary>
    public static string GetBlockedReason(StepPermissionInfo step) => step.ApproverRole is { } approver
        ? $"Bu adım {approver.ToDisplayName()} tarafından onaylanır."
        : $"Bu adım {step.ResponsibleRole.ToDisplayName()} tarafından yapılır.";

    private static AvailableActionsDto Blocked(string reason) => new(false, false, false, reason);
}
