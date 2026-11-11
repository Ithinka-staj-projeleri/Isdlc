using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Process;
using Isdlc.Application.Process.Dtos;
using Isdlc.Application.Tests.Common;
using Isdlc.Domain.Identity;
using Isdlc.Domain.Process;
using Isdlc.Domain.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Process;

/// <summary>
/// The five company flows as seeded (Infrastructure/Seed/flows/*.json, from Ithinka Proje Dokümanları/Akışlar).
/// Expectations come from the flow charts, docs/process-api.md and docs/data-model.md.
/// </summary>
[Collection("Database")]
public class ProcessServiceTests(DatabaseFixture database)
{
    private async Task<ProcessFlowDto> GetFlowAsync(string code)
    {
        await using var scope = database.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IProcessService>();
        return await service.GetFlowAsync(code, CancellationToken.None);
    }

    private async Task<IReadOnlyList<ProcessFlowListItemDto>> GetFlowsAsync()
    {
        await using var scope = database.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IProcessService>();
        return await service.GetFlowsAsync(CancellationToken.None);
    }

    private static List<ProcessStepDto> AllSteps(ProcessFlowDto flow) =>
        flow.Stages.SelectMany(stage => stage.Steps).ToList();

    private async Task<ProcessStepDto> StepAsync(string flowCode, string stepCode) =>
        AllSteps(await GetFlowAsync(flowCode)).Single(s => s.Code == stepCode);

    private static readonly string[] AllFlowCodes =
    [
        ProcessFlowCodes.NewRequest,
        ProcessFlowCodes.AddOn,
        ProcessFlowCodes.ProcessImprovement,
        ProcessFlowCodes.ResourcePlanning,
        ProcessFlowCodes.Billing,
    ];

    public static TheoryData<string> FlowCodes => new(AllFlowCodes);

    // ---- The five flows ----

    [Fact]
    public async Task GetFlows_ReturnsTheFiveCompanyFlowsInOrder()
    {
        var flows = await GetFlowsAsync();

        Assert.Equal(
            [
                ProcessFlowCodes.NewRequest, ProcessFlowCodes.AddOn, ProcessFlowCodes.ProcessImprovement,
                ProcessFlowCodes.ResourcePlanning, ProcessFlowCodes.Billing,
            ],
            flows.Select(f => f.Code));
        Assert.Equal([19, 17, 8, 8, 6], flows.Select(f => f.StepCount));
    }

    [Theory]
    [InlineData(ProcessFlowCodes.NewRequest, ProcessFlowKind.Request, 3)]
    [InlineData(ProcessFlowCodes.AddOn, ProcessFlowKind.Request, null)]
    [InlineData(ProcessFlowCodes.ProcessImprovement, ProcessFlowKind.Improvement, null)]
    [InlineData(ProcessFlowCodes.ResourcePlanning, ProcessFlowKind.ResourcePlanning, null)]
    [InlineData(ProcessFlowCodes.Billing, ProcessFlowKind.Billing, null)]
    public async Task GetFlows_EachFlowHasItsKindAndProjectStage(string code, ProcessFlowKind kind, int? projectStageOrder)
    {
        var flow = Assert.Single(await GetFlowsAsync(), f => f.Code == code);

        Assert.Equal(kind, flow.Kind);
        Assert.Equal(projectStageOrder, flow.ProjectStageOrder);
    }

    [Theory]
    [MemberData(nameof(FlowCodes))]
    public async Task GetFlow_HasFourStages_AndStepsNumberedInFlowOrder(string code)
    {
        var flow = await GetFlowAsync(code);
        var steps = AllSteps(flow);

        Assert.Equal([1, 2, 3, 4], flow.Stages.Select(s => s.Order));
        Assert.All(flow.Stages, stage => Assert.NotEmpty(stage.Steps));
        // Steps of all stages, read one after another, are 1, 2, 3, ... and their codes are the same numbers.
        Assert.Equal(Enumerable.Range(1, steps.Count), steps.Select(s => s.Order));
        Assert.Equal(steps.Select(s => s.Order.ToString()), steps.Select(s => s.Code));
    }

    [Fact]
    public async Task GetFlow_NewSoftwareProject_StagesFollowTheChart()
    {
        var flow = await GetFlowAsync(ProcessFlowCodes.NewRequest);

        Assert.Equal(
            ["Talep ve Ön Analiz", "Teklif ve Anlaşma", "Proje Başlatma ve Geliştirme", "Kapanış ve Destek"],
            flow.Stages.Select(s => s.Title));
        Assert.Equal([9, 2, 6, 2], flow.Stages.Select(s => s.Steps.Count));
        Assert.StartsWith("Talep alınır ve Proje No verilir.", AllSteps(flow)[0].Text);
        Assert.StartsWith("Proje kapatıldı.", AllSteps(flow)[^1].Text);
    }

    [Fact]
    public async Task GetFlow_UnknownCode_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => GetFlowAsync("isdlc-v12"));
    }

    // ---- Conditional steps ----

    [Fact]
    public async Task GetFlow_NewSoftwareProject_CustomerSuitabilityIsOnlyForExternalRequestsWithAnOffer()
    {
        // "Doğrudan proje" skips step 4; the chart also says it is done only for external customers.
        var step = await StepAsync(ProcessFlowCodes.NewRequest, "4");

        Assert.Equal(new StepConditionDto(RequiresExternalRequest: true, RequiresOffer: true, MinLevel: null), step.Condition);
    }

    [Theory]
    [InlineData("10")]
    [InlineData("11")]
    public async Task GetFlow_NewSoftwareProject_OfferStepsNeedAnOffer(string code)
    {
        var step = await StepAsync(ProcessFlowCodes.NewRequest, code);

        Assert.Equal(new StepConditionDto(RequiresExternalRequest: false, RequiresOffer: true, MinLevel: null), step.Condition);
    }

    [Theory]
    [InlineData("8", 2)]
    [InlineData("9", 3)]
    public async Task GetFlow_AddOn_ScopeFromLevel2_OfferFromLevel3(string code, int minLevel)
    {
        var step = await StepAsync(ProcessFlowCodes.AddOn, code);

        Assert.Equal(new StepConditionDto(RequiresExternalRequest: false, RequiresOffer: false, MinLevel: minLevel), step.Condition);
    }

    [Fact]
    public async Task GetFlows_OnlyTheChartedStepsAreConditional()
    {
        var conditional = new List<string>();
        foreach (var code in AllFlowCodes)
        {
            conditional.AddRange(AllSteps(await GetFlowAsync(code))
                .Where(s => s.Condition != new StepConditionDto(false, false, null))
                .Select(s => $"{code}/{s.Code}"));
        }

        Assert.Equal(
            ["new-software-project/4", "new-software-project/10", "new-software-project/11", "add-on/8", "add-on/9"],
            conditional);
    }

    // ---- Decisions, outcomes and returns ----

    [Theory]
    [InlineData(ProcessFlowCodes.NewRequest, "4", ProjectStatus.Rejected)]
    [InlineData(ProcessFlowCodes.NewRequest, "11", ProjectStatus.Lost)]
    [InlineData(ProcessFlowCodes.ProcessImprovement, "2", ProjectStatus.Rejected)]
    public async Task GetFlow_ClosingDecisions_HaveTheirOutcome(string flowCode, string code, ProjectStatus outcome)
    {
        var step = await StepAsync(flowCode, code);

        Assert.True(step.IsDecision);
        Assert.Equal(outcome, step.NoOutcome);
    }

    [Theory]
    [InlineData(ProcessFlowCodes.NewRequest, "10", "9")]
    [InlineData(ProcessFlowCodes.NewRequest, "17", "16")]
    [InlineData(ProcessFlowCodes.AddOn, "10", "9")]
    [InlineData(ProcessFlowCodes.ProcessImprovement, "4", "3")]
    [InlineData(ProcessFlowCodes.ResourcePlanning, "7", "6")]
    [InlineData(ProcessFlowCodes.Billing, "4", "3")]
    public async Task GetFlow_Returns_GoBackToTheChartedStep(string flowCode, string code, string target)
    {
        var step = await StepAsync(flowCode, code);

        Assert.True(step.IsDecision);
        Assert.Equal(target, Assert.Single(step.NoJumps).TargetStepCode);
    }

    [Fact]
    public async Task GetFlow_ProcessImprovementPilot_CanBeRepeatedOrRestarted()
    {
        // Step 7: not met → back to the study (3); partly met → repeat the pilot (5). Options are listed in flow order.
        var step = await StepAsync(ProcessFlowCodes.ProcessImprovement, "7");

        Assert.Equal(["3", "5"], step.NoJumps.Select(j => j.TargetStepCode));
    }

    [Theory]
    [MemberData(nameof(FlowCodes))]
    public async Task GetFlow_JumpTargetsAreEarlierStepsOfTheSameFlow(string code)
    {
        var steps = AllSteps(await GetFlowAsync(code));
        var stepsById = steps.ToDictionary(s => s.Id);

        Assert.All(steps.SelectMany(s => s.NoJumps.Select(j => (Step: s, Jump: j))), x =>
        {
            Assert.True(stepsById.TryGetValue(x.Jump.TargetStepId, out var target), $"{x.Step.Code} jumps out of the flow");
            Assert.Equal(target!.Code, x.Jump.TargetStepCode);
            Assert.True(target.Order < x.Step.Order, $"{x.Step.Code} jumps forward to {target.Code}");
        });
    }

    [Theory]
    [MemberData(nameof(FlowCodes))]
    public async Task GetFlow_OnlyDecisionStepsHaveOutcomesOrJumps_NeverBoth(string code)
    {
        // data-model.md: a decision step never has NoOutcome and jump options together.
        var steps = AllSteps(await GetFlowAsync(code));

        Assert.DoesNotContain(steps, s => s.NoOutcome is not null && s.NoJumps.Count > 0);
        Assert.All(steps.Where(s => !s.IsDecision), step =>
        {
            Assert.Null(step.NoOutcome);
            Assert.Empty(step.NoJumps);
        });
    }

    // ---- Roles ----

    [Fact]
    public async Task GetFlow_NewSoftwareProject_OfferIsPreparedBySalesAndApprovedByManager()
    {
        var step = await StepAsync(ProcessFlowCodes.NewRequest, "10");

        Assert.Equal(UserRole.SalesRepresentative, step.ResponsibleRole);
        Assert.Equal(UserRole.SoftwareDepartmentManager, step.ApproverRole);
    }

    [Fact]
    public async Task GetFlows_ApprovalSteps_AreTheChartedOnes()
    {
        var approvals = new List<string>();
        foreach (var code in AllFlowCodes)
        {
            approvals.AddRange(AllSteps(await GetFlowAsync(code))
                .Where(s => s.ApproverRole is not null)
                .Select(s => $"{code}/{s.Code}:{s.ApproverRole}"));
        }

        Assert.Equal(
            [
                "new-software-project/10:SoftwareDepartmentManager",
                "add-on/8:ProjectManager",
                "process-improvement/4:SoftwareDepartmentManager",
                "process-improvement/8:SoftwareDepartmentManager",
                "billing/4:ProjectManager",
            ],
            approvals);
    }
}
