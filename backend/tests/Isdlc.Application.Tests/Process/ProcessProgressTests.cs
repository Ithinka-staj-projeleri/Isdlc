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
    private static readonly StepPermissionInfo NormalStep = new(IsDecision: false, UserRole.BusinessAnalyst, ApproverRole: null);   // 1.1
    private static readonly StepPermissionInfo DecisionStep = new(IsDecision: true, UserRole.BusinessAnalyst, ApproverRole: null);  // 2.2
    private static readonly StepPermissionInfo ApprovalDecision = new(true, UserRole.BusinessAnalyst, UserRole.SoftwareDepartmentManager); // 4.5
    private static readonly StepPermissionInfo SalesApproval = new(false, UserRole.SalesRepresentative, UserRole.SoftwareDepartmentManager); // 5.1

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

    [Fact]
    public void AvailableActions_ResponsibleRoleOnNormalStep_CanComplete()
    {
        var actions = ProcessProgress.GetAvailableActions(ProjectStatus.Active, NormalStep, UserRole.BusinessAnalyst);

        Assert.Equal(new AvailableActionsDto(CanComplete: true, CanAnswer: false, CanReject: false, BlockedReason: null), actions);
    }

    [Fact]
    public void AvailableActions_ResponsibleRoleOnDecisionStep_CanAnswer()
    {
        var actions = ProcessProgress.GetAvailableActions(ProjectStatus.Active, DecisionStep, UserRole.BusinessAnalyst);

        Assert.False(actions.CanComplete);
        Assert.True(actions.CanAnswer);
        Assert.Null(actions.BlockedReason);
    }

    [Fact]
    public void AvailableActions_OtherRole_IsBlockedWithTheResponsibleRoleName()
    {
        var actions = ProcessProgress.GetAvailableActions(ProjectStatus.Active, NormalStep, UserRole.SoftwareDeveloper);

        Assert.False(actions.CanComplete);
        Assert.False(actions.CanAnswer);
        Assert.False(actions.CanReject);
        Assert.Equal("Bu adım İş Analisti tarafından yapılır.", actions.BlockedReason);
    }

    [Fact]
    public void AvailableActions_ApprovalStep_ResponsibleRoleCannotCloseIt()
    {
        // 4.5: the analyst prepares, only the manager answers.
        var actions = ProcessProgress.GetAvailableActions(ProjectStatus.Active, ApprovalDecision, UserRole.BusinessAnalyst);

        Assert.False(actions.CanAnswer);
        Assert.Equal("Bu adım Yazılım Departman Yöneticisi tarafından onaylanır.", actions.BlockedReason);
    }

    [Fact]
    public void AvailableActions_ApprovalStep_ManagerCanAnswerAndReject()
    {
        var actions = ProcessProgress.GetAvailableActions(ProjectStatus.Active, ApprovalDecision, UserRole.SoftwareDepartmentManager);

        Assert.Equal(new AvailableActionsDto(CanComplete: false, CanAnswer: true, CanReject: true, BlockedReason: null), actions);
    }

    [Fact]
    public void AvailableActions_ManagerOnAnalystStep_CannotActButCanReject()
    {
        // Rejecting is allowed on any step; acting on it is not.
        var actions = ProcessProgress.GetAvailableActions(ProjectStatus.Active, NormalStep, UserRole.SoftwareDepartmentManager);

        Assert.False(actions.CanComplete);
        Assert.True(actions.CanReject);
        Assert.Equal("Bu adım İş Analisti tarafından yapılır.", actions.BlockedReason);
    }

    [Theory]
    [InlineData(ProjectStatus.Closed)]
    [InlineData(ProjectStatus.Rejected)]
    [InlineData(ProjectStatus.Lost)]
    [InlineData(ProjectStatus.Redirected)]
    public void AvailableActions_InactiveProject_NothingAllowed(ProjectStatus status)
    {
        var actions = ProcessProgress.GetAvailableActions(status, NormalStep, UserRole.SoftwareDepartmentManager);

        Assert.Equal(new AvailableActionsDto(false, false, false, "Proje aktif değil; süreçte işlem yapılamaz."), actions);
    }

    [Fact]
    public void AvailableActions_NoSignedInUser_NothingAllowed()
    {
        var actions = ProcessProgress.GetAvailableActions(ProjectStatus.Active, NormalStep, userRole: null);

        Assert.Equal(new AvailableActionsDto(false, false, false, "İşlem yapmak için giriş yapılmalıdır."), actions);
    }

    [Fact]
    public void AvailableActions_NoCurrentStep_NothingAllowed()
    {
        var actions = ProcessProgress.GetAvailableActions(ProjectStatus.Active, currentStep: null, UserRole.BusinessAnalyst);

        Assert.False(actions.CanComplete || actions.CanAnswer || actions.CanReject);
        Assert.NotNull(actions.BlockedReason);
    }

    // ---- Permission rules used by the commands ----

    [Theory]
    [InlineData(UserRole.SalesRepresentative, false)] // prepares the offer, but cannot close the step
    [InlineData(UserRole.SoftwareDepartmentManager, true)]
    [InlineData(UserRole.BusinessAnalyst, false)]
    public void CanActOnStep_Step51_OnlyTheApproverClosesIt(UserRole role, bool expected)
    {
        Assert.Equal(expected, ProcessProgress.CanActOnStep(SalesApproval, role));
    }

    [Theory]
    [InlineData(UserRole.SoftwareDepartmentManager, true)]
    [InlineData(UserRole.BusinessAnalyst, false)]
    [InlineData(UserRole.ProjectManager, false)]
    [InlineData(UserRole.SalesRepresentative, false)]
    public void CanReject_OnlyTheManager(UserRole role, bool expected)
    {
        Assert.Equal(expected, ProcessProgress.CanReject(role));
    }

    [Fact]
    public void WhyCannotReject_OtherRole_ExplainsWhoCan()
    {
        Assert.Equal("Talebi yalnızca Yazılım Departman Yöneticisi reddedebilir.",
            ProcessProgress.WhyCannotReject(NormalStep, UserRole.BusinessAnalyst));
        Assert.Null(ProcessProgress.WhyCannotReject(NormalStep, UserRole.SoftwareDepartmentManager));
    }
}
