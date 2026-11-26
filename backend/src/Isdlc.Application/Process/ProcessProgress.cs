using Isdlc.Application.Identity;
using Isdlc.Application.Process.Dtos;
using Isdlc.Domain.Identity;
using Isdlc.Domain.Projects;

namespace Isdlc.Application.Process;

/// <summary>The current step's fields that decide who can act on it.</summary>
public record StepPermissionInfo(bool IsDecision, Permission ResponsiblePermission, Permission? ApproverPermission)
{
    /// <summary>An approval step is closed by its approver; any other step by its responsible permission.</summary>
    public Permission Required => ApproverPermission ?? ResponsiblePermission;
}

/// <summary>
/// Pure calculations for a project's place in the flow. No database access, so they can be unit tested directly.
/// "Reference step": the current step for an active project, the step of the last StepRecord otherwise.
/// Rules: docs/process-api.md → "Hesaplanan alanlar" and "Yetki kuralları".
/// </summary>
public static class ProcessProgress
{
    /// <summary>
    /// referenceStepPosition: 1-based position of the reference step among the steps done on the project
    /// (skipped steps are not counted); totalSteps: how many steps are done on the project.
    /// </summary>
    public static int CalculateProgressPercent(ProjectStatus status, int? referenceStepPosition, int totalSteps)
    {
        if (status == ProjectStatus.Closed)
        {
            return 100;
        }

        if (referenceStepPosition is null || totalSteps <= 0)
        {
            return 0;
        }

        // Steps before the reference step are done; integer division rounds down.
        return (referenceStepPosition.Value - 1) * 100 / totalSteps;
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

    /// <param name="user">The signed-in user's permissions; null when nobody (active) is signed in.</param>
    /// <param name="inRequestPhase">The record is a request (before its flow's project stage), so it can be postponed.</param>
    public static AvailableActionsDto GetAvailableActions(
        ProjectStatus status, StepPermissionInfo? currentStep, UserAccess? user, bool inRequestPhase = false)
    {
        if (status == ProjectStatus.Postponed)
        {
            if (user is not null && CanDecide(user))
            {
                return new AvailableActionsDto(false, false, false, false, CanResume: true,
                    "Talep ertelendi; değerlendirmeye geri alınınca bu adımdan devam eder.");
            }

            return Blocked(
                "Talep ertelendi; «Onay ve karar» yetkisi olan biri değerlendirmeye geri alınca devam eder.",
                user is null ? null : Permission.Approval);
        }

        if (status != ProjectStatus.Active)
        {
            return Blocked("Proje aktif değil; süreçte işlem yapılamaz.");
        }

        if (currentStep is null)
        {
            return Blocked("Projenin mevcut adımı bulunamadı.");
        }

        if (user is null)
        {
            return Blocked("İşlem yapmak için giriş yapılmalıdır.");
        }

        var canReject = CanDecide(user);
        var canPostpone = canReject && inRequestPhase;

        if (!CanActOnStep(currentStep, user))
        {
            return new AvailableActionsDto(
                false, false, canReject, canPostpone, false, GetBlockedReason(currentStep, user.TenantAdmins), currentStep.Required);
        }

        return new AvailableActionsDto(
            CanComplete: !currentStep.IsDecision,
            CanAnswer: currentStep.IsDecision,
            CanReject: canReject,
            CanPostpone: canPostpone,
            CanResume: false,
            BlockedReason: null);
    }

    /// <summary>
    /// Who can complete or answer a step: anyone with the step's permission (its approver's on an approval step).
    /// Used both for the buttons (GetAvailableActions) and for the check in the commands, so the two never disagree.
    /// </summary>
    public static bool CanActOnStep(StepPermissionInfo step, UserAccess user) => user.Has(step.Required);

    /// <summary>Rejecting, postponing and taking back a request need the approval permission.</summary>
    public static bool CanDecide(UserAccess user) => user.Has(Permission.Approval);

    /// <summary>Why the user cannot complete or answer the step; null when they can.</summary>
    public static string? WhyCannotActOnStep(StepPermissionInfo step, UserAccess user) =>
        CanActOnStep(step, user) ? null : GetBlockedReason(step, user.TenantAdmins);

    /// <summary>Why the user cannot reject, postpone or take back the request; null when they can. Allowed on any step.</summary>
    public static string? WhyCannotDecide(StepPermissionInfo step, UserAccess user) =>
        CanDecide(user) ? null : PermissionMessages.ForDecision(user.TenantAdmins);

    /// <summary>Message for a user who cannot act on the step: the permission it needs and whom to ask.</summary>
    public static string GetBlockedReason(StepPermissionInfo step, IReadOnlyList<string> tenantAdmins) =>
        step.ApproverPermission is not null
            ? PermissionMessages.ForApproval(tenantAdmins)
            : PermissionMessages.ForStep(step.ResponsiblePermission, tenantAdmins);

    private static AvailableActionsDto Blocked(string reason, Permission? missing = null) =>
        new(false, false, false, false, false, reason, missing);
}
