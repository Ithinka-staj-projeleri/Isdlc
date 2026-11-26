using System.Text.Json;
using Isdlc.Application.Process;
using Isdlc.Domain.Process;
using Isdlc.Domain.Projects;

namespace Isdlc.Application.Tests.Process;

/// <summary>
/// Which steps are done on a record and which step comes next, with skip rules (Faz 7 · A5). No database.
/// The first group keeps the Faz 4 cases with the three rules the migration wrote; the rest tries every field type,
/// comparison and empty value. Rules: docs/configurability.md → Atlama kuralları.
/// </summary>
public class StepConditionsTests
{
    private static readonly StepConditionRule[] Always = [];
    private static readonly StepConditionRule External = new(StepConditionRule.Source, ConditionOperator.Equals, "External");
    private static readonly StepConditionRule Offer = new(StepConditionRule.RequiresOffer, ConditionOperator.Equals, "true");

    private static StepConditionRule MinLevel(int level) =>
        new(StepConditionRule.Level, ConditionOperator.AtLeast, level.ToString(), ConditionWhenEmpty.Met);

    private static ConditionContext Context(
        RequestSource source = RequestSource.Internal, bool requiresOffer = false, int? level = null,
        RequestType requestType = RequestType.NewProject, Dictionary<string, string>? custom = null) =>
        new(source, requestType, requiresOffer, level, custom);

    // ---- The three Faz 4 conditions, as rules ----

    [Fact]
    public void Applies_StepWithoutRules_IsDoneOnEveryRecord()
    {
        Assert.True(StepConditions.Applies(Always, Context()));
        Assert.True(StepConditions.Applies(Always, Context(RequestSource.External, true, 3)));
    }

    [Theory]
    [InlineData(RequestSource.External, true)]
    [InlineData(RequestSource.Internal, false)]
    [InlineData(RequestSource.Affiliated, false)]
    public void Applies_ExternalOnlyStep_DependsOnTheSource(RequestSource source, bool expected)
    {
        Assert.Equal(expected, StepConditions.Applies([External], Context(source)));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Applies_OfferStep_DependsOnWhetherAnOfferIsPrepared(bool requiresOffer, bool expected)
    {
        Assert.Equal(expected, StepConditions.Applies([Offer], Context(requiresOffer: requiresOffer)));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public void Applies_LevelStep_IsDoneFromItsMinimumLevel(int level, bool expected)
    {
        Assert.Equal(expected, StepConditions.Applies([MinLevel(2)], Context(level: level)));
    }

    [Fact]
    public void Applies_LevelStep_IsDoneWhileTheLevelIsNotDecided()
    {
        Assert.True(StepConditions.Applies([MinLevel(3)], Context(level: null)));
    }

    [Theory]
    [InlineData(RequestSource.External, true, 3, true)]
    [InlineData(RequestSource.Internal, true, 3, false)]
    [InlineData(RequestSource.External, false, 3, false)]
    [InlineData(RequestSource.External, true, 2, false)]
    public void Applies_StepWithSeveralRules_NeedsAllOfThem(
        RequestSource source, bool requiresOffer, int level, bool expected)
    {
        Assert.Equal(expected, StepConditions.Applies([External, Offer, MinLevel(3)], Context(source, requiresOffer, level)));
    }

    // ---- Comparisons, fields and empty values ----

    [Theory]
    [InlineData(ConditionOperator.Equals, "AdditionalModule", true)]
    [InlineData(ConditionOperator.Equals, "NewProject", false)]
    [InlineData(ConditionOperator.NotEquals, "NewProject", true)]
    [InlineData(ConditionOperator.NotEquals, "AdditionalModule", false)]
    public void Holds_RequestType_ComparesTheChoice(ConditionOperator op, string value, bool expected)
    {
        var rule = new StepConditionRule(StepConditionRule.RequestType, op, value);

        Assert.Equal(expected, StepConditions.Holds(rule, Context(requestType: RequestType.AdditionalModule)));
    }

    [Fact]
    public void Holds_Equals_IgnoresCaseAndSpaces()
    {
        Assert.True(StepConditions.Holds(new StepConditionRule(StepConditionRule.Source, ConditionOperator.Equals, " external "),
            Context(RequestSource.External)));
    }

    [Theory]
    [InlineData(ConditionOperator.AtLeast, "2", 2, true)]
    [InlineData(ConditionOperator.AtLeast, "3", 2, false)]
    [InlineData(ConditionOperator.AtMost, "2", 2, true)]
    [InlineData(ConditionOperator.AtMost, "1", 2, false)]
    public void Holds_Level_ComparesNumbers(ConditionOperator op, string value, int level, bool expected)
    {
        Assert.Equal(expected, StepConditions.Holds(new StepConditionRule(StepConditionRule.Level, op, value), Context(level: level)));
    }

    [Theory]
    [InlineData(ConditionWhenEmpty.NotMet, false)]
    [InlineData(ConditionWhenEmpty.Met, true)]
    public void Holds_EmptyField_GivesTheRulesWhenEmpty(ConditionWhenEmpty whenEmpty, bool expected)
    {
        var rule = new StepConditionRule(StepConditionRule.Level, ConditionOperator.AtLeast, "2", whenEmpty);

        Assert.Equal(expected, StepConditions.Holds(rule, Context(level: null)));
    }

    [Fact]
    public void Holds_IsSetAndIsNotSet_AskWhetherTheFieldIsFilled()
    {
        var isSet = new StepConditionRule(StepConditionRule.Level, ConditionOperator.IsSet);
        var isNotSet = new StepConditionRule(StepConditionRule.Level, ConditionOperator.IsNotSet, WhenEmpty: ConditionWhenEmpty.Met);

        Assert.True(StepConditions.Holds(isSet, Context(level: 1)));
        Assert.False(StepConditions.Holds(isSet, Context(level: null)));
        Assert.True(StepConditions.Holds(isNotSet, Context(level: null)));
        Assert.False(StepConditions.Holds(isNotSet, Context(level: 1)));
    }

    public static TheoryData<string, ConditionOperator, string, bool> CustomFieldCases => new()
    {
        // Evet-hayır, seçim, sayı, tarih.
        { "true", ConditionOperator.Equals, "true", true },
        { "false", ConditionOperator.Equals, "true", false },
        { "Altın", ConditionOperator.Equals, "Altın", true },
        { "Altın", ConditionOperator.NotEquals, "Gümüş", true },
        { "250000", ConditionOperator.AtLeast, "100000", true },
        { "50000", ConditionOperator.AtLeast, "100000", false },
        { "2026-12-01", ConditionOperator.AtMost, "2026-12-31", true },
        { "2027-01-15", ConditionOperator.AtMost, "2026-12-31", false },
    };

    [Theory]
    [MemberData(nameof(CustomFieldCases))]
    public void Holds_CustomField_ComparesItsValue(string recordValue, ConditionOperator op, string ruleValue, bool expected)
    {
        var rule = new StepConditionRule("custom:butce", op, ruleValue);

        Assert.Equal(expected, StepConditions.Holds(rule, Context(custom: new() { ["butce"] = recordValue })));
    }

    [Fact]
    public void Holds_CustomFieldTheRecordHasNoValueFor_IsEmpty()
    {
        var rule = new StepConditionRule("custom:butce", ConditionOperator.Equals, "true");

        Assert.False(StepConditions.Holds(rule, Context()));
        Assert.False(StepConditions.Holds(rule, Context(custom: new() { ["butce"] = "  " })));
        Assert.True(StepConditions.Holds(rule with { WhenEmpty = ConditionWhenEmpty.Met }, Context()));
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("custom:")]
    public void Holds_FieldTheRecordDoesNotHave_DoesNotHold(string field)
    {
        Assert.False(StepConditions.Holds(new StepConditionRule(field, ConditionOperator.Equals, "x"), Context()));
    }

    [Fact]
    public void Holds_TextCannotBeComparedAsANumber_DoesNotHold()
    {
        var rule = new StepConditionRule("custom:not", ConditionOperator.AtLeast, "10");

        Assert.False(StepConditions.Holds(rule, Context(custom: new() { ["not"] = "on" })));
    }

    [Fact]
    public void DependsOn_FindsTheFieldAmongTheRules()
    {
        Assert.True(StepConditions.DependsOn([External, MinLevel(2)], StepConditionRule.Source));
        Assert.False(StepConditions.DependsOn([MinLevel(2)], StepConditionRule.RequiresOffer));
    }

    // ---- Labels ----

    [Fact]
    public void Describe_TheThreeFaz4Rules_KeepTheirLabels()
    {
        Assert.Equal(["Yalnızca dış talepte", "Teklif hazırlanacaksa", "Seviye 2 ve üstü"],
            StepConditions.Describe([External, Offer, MinLevel(2)]));
    }

    [Theory]
    [InlineData(StepConditionRule.Source, ConditionOperator.NotEquals, "Internal", "Talep kaynağı ≠ Kurum içi")]
    [InlineData(StepConditionRule.RequiresOffer, ConditionOperator.Equals, "false", "Teklif = hazırlanmayacak")]
    [InlineData(StepConditionRule.Level, ConditionOperator.AtMost, "1", "Seviye ≤ 1")]
    [InlineData("custom:butce-onayli", ConditionOperator.Equals, "true", "butce-onayli = Evet")]
    [InlineData("custom:sozlesme-no", ConditionOperator.IsSet, null, "sozlesme-no doluysa")]
    public void Describe_OtherRules_ReadAsText(string field, ConditionOperator op, string? value, string expected)
    {
        Assert.Equal(expected, StepConditions.Describe(new StepConditionRule(field, op, value)));
    }

    // ---- JSON: the seed files and the column ----

    [Fact]
    public void Json_ValuesKeepTheirKind_AndReadBackTheSame()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        };
        StepConditionRule[] rules = [External, Offer, MinLevel(2)];

        var json = JsonSerializer.Serialize(rules, options);

        Assert.Contains("\"value\":\"External\"", json);
        Assert.Contains("\"value\":true", json);
        Assert.Contains("\"value\":2", json);
        Assert.Equal(rules, JsonSerializer.Deserialize<StepConditionRule[]>(json, options));
    }

    // ---- A flow: next, first and skipped steps ----

    // Like the new software project flow: 3, then 4 only for external requests,
    // then 5, then 10 and 11 only when an offer is prepared, then 12.
    private static readonly IReadOnlyList<ConditionalStep> Flow =
    [
        new(Id: 103, Order: 3, Always),
        new(Id: 104, Order: 4, [External]),
        new(Id: 105, Order: 5, Always),
        new(Id: 110, Order: 10, [Offer]),
        new(Id: 111, Order: 11, [Offer]),
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
            new(Id: 2, Order: 2, [Offer]),
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
    public void FirstStepId_SkipsLeadingStepsWhoseRulesDoNotHold()
    {
        IReadOnlyList<ConditionalStep> flow =
        [
            new(Id: 1, Order: 1, [External]),
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
