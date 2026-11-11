using Isdlc.Application.Process;
using Isdlc.Domain.Projects;

namespace Isdlc.Application.Tests.Process;

/// <summary>
/// Which steps are done on a record and which step comes next. No database.
/// Rules: docs/process-api.md → "Adım koşulları".
/// </summary>
public class StepConditionsTests
{
    private static readonly StepCondition Always = new(false, false, null);

    private static ConditionContext Context(
        RequestSource source = RequestSource.Internal, bool requiresOffer = false, int? level = null) =>
        new(source, requiresOffer, level);

    [Fact]
    public void Applies_StepWithoutConditions_IsDoneOnEveryRecord()
    {
        Assert.True(StepConditions.Applies(Always, Context()));
        Assert.True(StepConditions.Applies(Always, Context(RequestSource.External, true, 3)));
    }

    [Theory]
    [InlineData(RequestSource.External, true)]
    [InlineData(RequestSource.Internal, false)]
    public void Applies_ExternalOnlyStep_DependsOnTheSource(RequestSource source, bool expected)
    {
        var condition = new StepCondition(RequiresExternalRequest: true, RequiresOffer: false, MinLevel: null);

        Assert.Equal(expected, StepConditions.Applies(condition, Context(source)));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Applies_OfferStep_DependsOnWhetherAnOfferIsPrepared(bool requiresOffer, bool expected)
    {
        var condition = new StepCondition(RequiresExternalRequest: false, RequiresOffer: true, MinLevel: null);

        Assert.Equal(expected, StepConditions.Applies(condition, Context(requiresOffer: requiresOffer)));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public void Applies_LevelStep_IsDoneFromItsMinimumLevel(int level, bool expected)
    {
        var condition = new StepCondition(RequiresExternalRequest: false, RequiresOffer: false, MinLevel: 2);

        Assert.Equal(expected, StepConditions.Applies(condition, Context(level: level)));
    }

    [Fact]
    public void Applies_LevelStep_IsDoneWhileTheLevelIsNotDecided()
    {
        var condition = new StepCondition(RequiresExternalRequest: false, RequiresOffer: false, MinLevel: 3);

        Assert.True(StepConditions.Applies(condition, Context(level: null)));
    }

    [Theory]
    [InlineData(RequestSource.External, true, 3, true)]
    [InlineData(RequestSource.Internal, true, 3, false)]
    [InlineData(RequestSource.External, false, 3, false)]
    [InlineData(RequestSource.External, true, 2, false)]
    public void Applies_StepWithSeveralConditions_NeedsAllOfThem(
        RequestSource source, bool requiresOffer, int level, bool expected)
    {
        var condition = new StepCondition(RequiresExternalRequest: true, RequiresOffer: true, MinLevel: 3);

        Assert.Equal(expected, StepConditions.Applies(condition, Context(source, requiresOffer, level)));
    }

    // A flow like the new software project flow: 3, then 4 only for external requests,
    // then 5, then 10 and 11 only when an offer is prepared, then 12.
    private static readonly IReadOnlyList<ConditionalStep> Flow =
    [
        new(Id: 103, Order: 3, Always),
        new(Id: 104, Order: 4, new StepCondition(true, false, null)),
        new(Id: 105, Order: 5, Always),
        new(Id: 110, Order: 10, new StepCondition(false, true, null)),
        new(Id: 111, Order: 11, new StepCondition(false, true, null)),
        new(Id: 112, Order: 12, Always),
    ];

    [Fact]
    public void NextStepId_InternalRequest_SkipsTheExternalOnlyStep()
    {
        Assert.Equal(105, StepConditions.NextStepId(Flow, currentOrder: 3, Context(RequestSource.Internal)));
    }

    [Fact]
    public void NextStepId_ExternalRequest_GoesToTheExternalOnlyStep()
    {
        Assert.Equal(104, StepConditions.NextStepId(Flow, currentOrder: 3, Context(RequestSource.External)));
    }

    [Fact]
    public void NextStepId_WithoutOffer_SkipsEveryOfferStep()
    {
        Assert.Equal(112, StepConditions.NextStepId(Flow, currentOrder: 5, Context(requiresOffer: false)));
    }

    [Fact]
    public void NextStepId_WithOffer_GoesToTheOfferStep()
    {
        Assert.Equal(110, StepConditions.NextStepId(Flow, currentOrder: 5, Context(requiresOffer: true)));
    }

    [Fact]
    public void NextStepId_AfterTheLastStep_IsNull()
    {
        Assert.Null(StepConditions.NextStepId(Flow, currentOrder: 12, Context()));
    }

    [Fact]
    public void NextStepId_WhenEveryLaterStepIsSkipped_IsNull()
    {
        IReadOnlyList<ConditionalStep> flow =
        [
            new(Id: 1, Order: 1, Always),
            new(Id: 2, Order: 2, new StepCondition(false, true, null)),
        ];

        Assert.Null(StepConditions.NextStepId(flow, currentOrder: 1, Context(requiresOffer: false)));
    }

    [Fact]
    public void NextStepId_DoesNotDependOnTheListOrder()
    {
        var shuffled = Flow.Reverse().ToList();

        Assert.Equal(105, StepConditions.NextStepId(shuffled, currentOrder: 3, Context(RequestSource.Internal)));
    }

    [Fact]
    public void FirstStepId_SkipsLeadingStepsWhoseConditionsDoNotHold()
    {
        IReadOnlyList<ConditionalStep> flow =
        [
            new(Id: 1, Order: 1, new StepCondition(true, false, null)),
            new(Id: 2, Order: 2, Always),
        ];

        Assert.Equal(2, StepConditions.FirstStepId(flow, Context(RequestSource.Internal)));
        Assert.Equal(1, StepConditions.FirstStepId(flow, Context(RequestSource.External)));
    }

    [Fact]
    public void FirstStepId_EmptyFlow_IsNull()
    {
        Assert.Null(StepConditions.FirstStepId([], Context()));
    }

    [Fact]
    public void SkippedStepIds_ListsTheStepsNotDoneOnTheRecord_InFlowOrder()
    {
        Assert.Equal([104, 110, 111], StepConditions.SkippedStepIds(Flow, Context(RequestSource.Internal)));
        Assert.Empty(StepConditions.SkippedStepIds(Flow, Context(RequestSource.External, requiresOffer: true)));
    }
}
