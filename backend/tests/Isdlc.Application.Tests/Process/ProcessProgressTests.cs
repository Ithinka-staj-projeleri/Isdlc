using Isdlc.Application.Identity;
using Isdlc.Application.Process;
using Isdlc.Application.Process.Dtos;
using Isdlc.Domain.Identity;
using Isdlc.Domain.Projects;

namespace Isdlc.Application.Tests.Process;

/// <summary>
/// Pure rules for a project's place in the flow (no database).
/// Expectations: docs/process-api.md → "Hesaplanan alanlar" and "Yetki kuralları".
/// </summary>
public class ProcessProgressTests
{
    // Step shapes from the seed (process-flow.json).
    private static readonly StepPermissionInfo NormalStep = new(IsDecision: false, Permission.Analysis, ApproverPermission: null);   // 1.1
    private static readonly StepPermissionInfo DecisionStep = new(IsDecision: true, Permission.Analysis, ApproverPermission: null);  // 2.2
    private static readonly StepPermissionInfo ApprovalDecision = new(true, Permission.Analysis, Permission.Approval); // 4.5
    private static readonly StepPermissionInfo SalesApproval = new(false, Permission.Sales, Permission.Approval); // 5.1

    // ---- Progress percent ----

    [Theory]
    [InlineData(1, 0)]   // first step: nothing done yet
    [InlineData(2, 1)]   // 1/75 = 1.33 → rounds down
    [InlineData(16, 20)] // 15/75 = 20
    [InlineData(75, 98)] // last step not done yet: 74/75 = 98.6 → 98
    public void ProgressPercent_ActiveProject_CountsStepsBeforeTheCurrentOne(int currentStepOrder, int expected)
    {
        Assert.Equal(expected, ProcessProgress.CalculateProgressPercent(ProjectStatus.Active, currentStepOrder, 75));
    }

    [Fact]
    public void ProgressPercent_ClosedProject_Is100()
    {
        Assert.Equal(100, ProcessProgress.CalculateProgressPercent(ProjectStatus.Closed, 75, 75));
    }

    [Fact]
    public void ProgressPercent_StoppedProject_UsesTheStepItStoppedOn()
    {
        // Lost at 5.3 (order 20): 19 steps done.
        Assert.Equal(25, ProcessProgress.CalculateProgressPercent(ProjectStatus.Lost, 20, 75));
    }

    [Theory]
    [InlineData(null, 75)]
    [InlineData(10, 0)]
    public void ProgressPercent_WithoutReferenceOrSteps_IsZero(int? referenceStepOrder, int totalSteps)
    {
        Assert.Equal(0, ProcessProgress.CalculateProgressPercent(ProjectStatus.Active, referenceStepOrder, totalSteps));
    }

    // ---- Stage states ----

    [Theory]
    [InlineData(3, ProcessStageState.Completed)]
    [InlineData(4, ProcessStageState.Current)]
    [InlineData(5, ProcessStageState.Pending)]
    public void StageState_ActiveProject_IsRelativeToTheCurrentStage(int stageOrder, ProcessStageState expected)
    {
        Assert.Equal(expected, ProcessProgress.GetStageState(ProjectStatus.Active, stageOrder, referenceStageOrder: 4));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(13)]
    public void StageState_ClosedProject_AllStagesCompleted(int stageOrder)
    {
        Assert.Equal(ProcessStageState.Completed, ProcessProgress.GetStageState(ProjectStatus.Closed, stageOrder, 13));
    }

    [Theory]
    [InlineData(ProjectStatus.Rejected)]
    [InlineData(ProjectStatus.Lost)]
    [InlineData(ProjectStatus.Redirected)]
    public void StageState_StoppedProject_StageOfTheLastRecordIsStopped(ProjectStatus status)
    {
        Assert.Equal(ProcessStageState.Completed, ProcessProgress.GetStageState(status, 4, referenceStageOrder: 5));
        Assert.Equal(ProcessStageState.Stopped, ProcessProgress.GetStageState(status, 5, referenceStageOrder: 5));
        Assert.Equal(ProcessStageState.Pending, ProcessProgress.GetStageState(status, 6, referenceStageOrder: 5));
    }

    [Fact]
    public void StageState_WithoutReference_IsPending()
    {
        Assert.Equal(ProcessStageState.Pending, ProcessProgress.GetStageState(ProjectStatus.Active, 1, null));
    }

    // ---- Who can act (buttons) ----

    private static readonly string[] Admins = ["Deniz Aydın", "Mert Kaya"];

    /// <summary>A signed-in user with these permissions; the company's administrators are Deniz Aydın and Mert Kaya.</summary>
    private static UserAccess As(params Permission[] permissions) => new(1, permissions.ToHashSet(), Admins);

    private const string AskAdmins = "Şirket yöneticinizden isteyin: Deniz Aydın, Mert Kaya.";

    [Fact]
    public void AvailableActions_ResponsiblePermissionOnNormalStep_CanComplete()
    {
        var actions = ProcessProgress.GetAvailableActions(ProjectStatus.Active, NormalStep, As(Permission.Analysis));

        Assert.Equal(new AvailableActionsDto(CanComplete: true, CanAnswer: false, CanReject: false, CanPostpone: false, CanResume: false, BlockedReason: null), actions);
    }

    [Fact]
    public void AvailableActions_ResponsiblePermissionOnDecisionStep_CanAnswer()
    {
        var actions = ProcessProgress.GetAvailableActions(ProjectStatus.Active, DecisionStep, As(Permission.Analysis));

        Assert.False(actions.CanComplete);
        Assert.True(actions.CanAnswer);
        Assert.Null(actions.BlockedReason);
    }

    [Fact]
    public void AvailableActions_WithoutThePermission_NamesItAndTheAdministrators()
    {
        var actions = ProcessProgress.GetAvailableActions(ProjectStatus.Active, NormalStep, As(Permission.Development));

        Assert.False(actions.CanComplete);
        Assert.False(actions.CanAnswer);
        Assert.False(actions.CanReject);
        Assert.Equal($"Bu adım için «Analiz adımları» yetkisi gerekir. {AskAdmins}", actions.BlockedReason);
        Assert.Equal(Permission.Analysis, actions.MissingPermission);
    }

    [Fact]
    public void AvailableActions_NoPermissionsAtAll_IsBlocked()
    {
        var actions = ProcessProgress.GetAvailableActions(ProjectStatus.Active, NormalStep, As());

        Assert.False(actions.CanComplete);
        Assert.Equal(Permission.Analysis, actions.MissingPermission);
    }

    [Fact]
    public void AvailableActions_ApprovalStep_ResponsiblePermissionCannotCloseIt()
    {
        // 4.5: the analyst prepares, only someone with "Onay ve karar" answers.
        var actions = ProcessProgress.GetAvailableActions(ProjectStatus.Active, ApprovalDecision, As(Permission.Analysis));

        Assert.False(actions.CanAnswer);
        Assert.Equal($"Bu adımı onaylamak için «Onay ve karar» yetkisi gerekir. {AskAdmins}", actions.BlockedReason);
        Assert.Equal(Permission.Approval, actions.MissingPermission);
    }

    [Fact]
    public void AvailableActions_ApprovalStep_ApproverCanAnswerAndReject()
    {
        var actions = ProcessProgress.GetAvailableActions(ProjectStatus.Active, ApprovalDecision, As(Permission.Approval));

        Assert.Equal(new AvailableActionsDto(CanComplete: false, CanAnswer: true, CanReject: true, CanPostpone: false, CanResume: false, BlockedReason: null), actions);
    }

    [Fact]
    public void AvailableActions_ApproverOnAnalysisStep_CannotActButCanReject()
    {
        // Rejecting is allowed on any step; acting on it is not.
        var actions = ProcessProgress.GetAvailableActions(ProjectStatus.Active, NormalStep, As(Permission.Approval));

        Assert.False(actions.CanComplete);
        Assert.True(actions.CanReject);
        Assert.Equal(Permission.Analysis, actions.MissingPermission);
    }

    [Fact]
    public void AvailableActions_SeveralPermissions_DoBoth()
    {
        // One person with both permissions closes the analysis step and can decide on the request.
        var actions = ProcessProgress.GetAvailableActions(
            ProjectStatus.Active, NormalStep, As(Permission.Analysis, Permission.Approval), inRequestPhase: true);

        Assert.Equal(new AvailableActionsDto(CanComplete: true, CanAnswer: false, CanReject: true, CanPostpone: true, CanResume: false, BlockedReason: null), actions);
    }

    [Theory]
    [InlineData(ProjectStatus.Closed)]
    [InlineData(ProjectStatus.Rejected)]
    [InlineData(ProjectStatus.Lost)]
    [InlineData(ProjectStatus.Redirected)]
    public void AvailableActions_InactiveProject_NothingAllowed(ProjectStatus status)
    {
        var actions = ProcessProgress.GetAvailableActions(status, NormalStep, As(Permission.Approval));

        Assert.Equal(new AvailableActionsDto(false, false, false, false, false, "Proje aktif değil; süreçte işlem yapılamaz."), actions);
    }

    // ---- Postpone and resume (D2) ----

    [Theory]
    [InlineData(true, true)]   // a request: can be postponed
    [InlineData(false, false)] // past the request stages: only rejected
    public void AvailableActions_Approver_CanPostponeOnlyARequest(bool inRequestPhase, bool canPostpone)
    {
        var actions = ProcessProgress.GetAvailableActions(
            ProjectStatus.Active, NormalStep, As(Permission.Approval), inRequestPhase);

        Assert.True(actions.CanReject);
        Assert.Equal(canPostpone, actions.CanPostpone);
        Assert.False(actions.CanResume);
    }

    [Fact]
    public void AvailableActions_WithoutApprovalOnARequest_CannotPostpone()
    {
        var actions = ProcessProgress.GetAvailableActions(ProjectStatus.Active, NormalStep, As(Permission.Analysis), inRequestPhase: true);

        Assert.False(actions.CanPostpone);
    }

    [Fact]
    public void AvailableActions_PostponedForApprover_OnlyResume()
    {
        var actions = ProcessProgress.GetAvailableActions(ProjectStatus.Postponed, NormalStep, As(Permission.Approval), true);

        Assert.Equal(
            new AvailableActionsDto(false, false, false, false, CanResume: true,
                "Talep ertelendi; değerlendirmeye geri alınınca bu adımdan devam eder."),
            actions);
    }

    [Fact]
    public void AvailableActions_PostponedWithoutApproval_NothingAllowed()
    {
        var actions = ProcessProgress.GetAvailableActions(ProjectStatus.Postponed, NormalStep, As(Permission.Analysis), true);

        Assert.Equal(
            new AvailableActionsDto(false, false, false, false, false,
                "Talep ertelendi; «Onay ve karar» yetkisi olan biri değerlendirmeye geri alınca devam eder.",
                Permission.Approval),
            actions);
    }

    [Fact]
    public void AvailableActions_NoSignedInUser_NothingAllowed()
    {
        var actions = ProcessProgress.GetAvailableActions(ProjectStatus.Active, NormalStep, user: null);

        Assert.Equal(new AvailableActionsDto(false, false, false, false, false, "İşlem yapmak için giriş yapılmalıdır."), actions);
    }

    [Fact]
    public void AvailableActions_NoCurrentStep_NothingAllowed()
    {
        var actions = ProcessProgress.GetAvailableActions(ProjectStatus.Active, currentStep: null, As(Permission.Analysis));

        Assert.False(actions.CanComplete || actions.CanAnswer || actions.CanReject);
        Assert.NotNull(actions.BlockedReason);
    }

    // ---- Permission rules used by the commands ----

    [Theory]
    [InlineData(Permission.Sales, false)] // prepares the offer, but cannot close the step
    [InlineData(Permission.Approval, true)]
    [InlineData(Permission.Analysis, false)]
    public void CanActOnStep_Step51_OnlyTheApproverClosesIt(Permission permission, bool expected)
    {
        Assert.Equal(expected, ProcessProgress.CanActOnStep(SalesApproval, As(permission)));
    }

    [Theory]
    [InlineData(Permission.Approval, true)]
    [InlineData(Permission.Analysis, false)]
    [InlineData(Permission.ProjectManagement, false)]
    [InlineData(Permission.Sales, false)]
    public void CanDecide_OnlyWithApproval(Permission permission, bool expected)
    {
        Assert.Equal(expected, ProcessProgress.CanDecide(As(permission)));
    }

    [Fact]
    public void WhyCannotDecide_WithoutApproval_NamesThePermissionAndTheAdministrators()
    {
        Assert.Equal($"Talep hakkında karar vermek için «Onay ve karar» yetkisi gerekir. {AskAdmins}",
            ProcessProgress.WhyCannotDecide(NormalStep, As(Permission.Analysis)));
        Assert.Null(ProcessProgress.WhyCannotDecide(NormalStep, As(Permission.Approval)));
    }

    [Fact]
    public void WhyCannotActOnStep_CompanyWithoutAdministrators_EndsTheSentence()
    {
        var user = new UserAccess(1, new HashSet<Permission>(), []);

        Assert.Equal("Bu adım için «Analiz adımları» yetkisi gerekir. Şirket yöneticinizden isteyin.",
            ProcessProgress.WhyCannotActOnStep(NormalStep, user));
    }
}
