using Isdlc.Application.Process;
using Isdlc.Domain.Projects;

namespace Isdlc.Application.Tests.Process;

/// <summary>
/// Where a project goes after an action (no database).
/// Expectations: docs/data-model.md → "Süreç ilerleme kuralları".
/// </summary>
public class ProcessTransitionsTests
{
    [Fact]
    public void AfterCompleted_MovesToTheNextStep()
    {
        Assert.Equal(new ProcessTransition(ProjectStatus.Active, 12), ProcessTransitions.AfterCompleted(nextStepId: 12));
    }

    [Fact]
    public void AfterCompleted_LastStep_ClosesTheProject()
    {
        Assert.Equal(new ProcessTransition(ProjectStatus.Closed, null), ProcessTransitions.AfterCompleted(nextStepId: null));
    }

    [Fact]
    public void AfterYes_MovesOnLikeCompleting()
    {
        Assert.Equal(ProcessTransitions.AfterCompleted(12), ProcessTransitions.AfterYes(12));
        Assert.Equal(ProcessTransitions.AfterCompleted(null), ProcessTransitions.AfterYes(null));
    }

    [Theory]
    [InlineData(ProjectStatus.Redirected)] // 1.2
    [InlineData(ProjectStatus.Rejected)]   // 3.2
    [InlineData(ProjectStatus.Lost)]       // 5.3
    public void AfterNo_WithOutcome_ClosesTheProjectWithThatStatus(ProjectStatus outcome)
    {
        Assert.Equal(new ProcessTransition(outcome, null), ProcessTransitions.AfterNo(5, outcome, jumpTargetStepId: null));
    }

    [Fact]
    public void AfterNo_WithReturnStep_GoesBackAndRecordsTheJump()
    {
        // 4.7 → 4.1
        var transition = ProcessTransitions.AfterNo(currentStepId: 15, noOutcome: null, jumpTargetStepId: 9);

        Assert.Equal(new ProcessTransition(ProjectStatus.Active, 9, JumpedToStepId: 9), transition);
    }

    [Fact]
    public void AfterNo_WithoutOutcomeOrReturnStep_StaysOnTheSameStep()
    {
        // 2.2: answered again once the missing information arrives.
        var transition = ProcessTransitions.AfterNo(currentStepId: 5, noOutcome: null, jumpTargetStepId: null);

        Assert.Equal(new ProcessTransition(ProjectStatus.Active, 5), transition);
        Assert.Null(transition.JumpedToStepId);
    }

    [Fact]
    public void AfterRejected_ClosesAsRejected()
    {
        Assert.Equal(new ProcessTransition(ProjectStatus.Rejected, null), ProcessTransitions.AfterRejected());
    }

    // ---- Validating the return step of a "No" answer ----

    [Theory]
    [InlineData(9)]
    [InlineData(17)]
    public void ValidateNoAnswer_OneOfTheOptions_IsValid(int chosen)
    {
        // 5.4 → 4.1 or 4.9
        Assert.Null(ProcessTransitions.ValidateNoAnswer([9, 17], chosen));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(10)]
    public void ValidateNoAnswer_StepWithOptions_NeedsOneOfThem(int? chosen)
    {
        Assert.Equal("Geçerli bir dönüş seçeneği seçilmelidir.", ProcessTransitions.ValidateNoAnswer([9, 17], chosen));
    }

    [Fact]
    public void ValidateNoAnswer_StepWithoutOptions_AcceptsNoReturnStep()
    {
        Assert.Null(ProcessTransitions.ValidateNoAnswer([], null));
    }

    [Fact]
    public void ValidateNoAnswer_StepWithoutOptions_RejectsAReturnStep()
    {
        Assert.Equal("Bu adımda dönüş seçeneği yok; dönüş adımı seçilmez.", ProcessTransitions.ValidateNoAnswer([], 9));
    }
}
