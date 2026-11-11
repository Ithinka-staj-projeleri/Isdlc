using Isdlc.Application.Process;
using Isdlc.Application.Process.Dtos;
using Isdlc.Application.Tests.Common;
using Isdlc.Domain.Identity;
using Isdlc.Domain.Process;
using Isdlc.Domain.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Process;

/// <summary>
/// ProcessService against the real seed (Infrastructure/Seed/flows/isdlc-v12.json, the flow new requests follow).
/// Expectations come from docs/data-model.md and docs/process-api.md.
/// </summary>
[Collection("Database")]
public class ProcessServiceTests(DatabaseFixture database)
{
    private async Task<IReadOnlyList<ProcessStageDto>> GetStagesAsync()
    {
        await using var scope = database.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IProcessService>();
        return (await service.GetFlowAsync(ProcessFlowCodes.NewRequest, CancellationToken.None)).Stages;
    }

    private static List<ProcessStepDto> AllSteps(IReadOnlyList<ProcessStageDto> stages) =>
        stages.SelectMany(stage => stage.Steps).ToList();

    [Fact]
    public async Task GetFlow_Returns13StagesInOrder()
    {
        var stages = await GetStagesAsync();

        Assert.Equal(Enumerable.Range(1, 13), stages.Select(s => s.Order));
        Assert.Equal("Talebin Kayda Alınması", stages[0].Title);
        Assert.Equal("Talep Kaydı", stages[0].ShortTitle);
    }

    [Fact]
    public async Task GetFlow_Returns75StepsInFlowOrder()
    {
        var steps = AllSteps(await GetStagesAsync());

        Assert.Equal(75, steps.Count);
        // Steps of all stages, read one after another, must follow the flow: 1, 2, ..., 75.
        Assert.Equal(Enumerable.Range(1, 75), steps.Select(s => s.Order));
        Assert.Equal("1.1", steps.First().Code);
        Assert.Equal("13.5", steps.Last().Code);
    }

    [Fact]
    public async Task GetFlow_EachStepIsUnderItsOwnStage()
    {
        var stages = await GetStagesAsync();

        // Step code "4.7" belongs to stage 4.
        Assert.All(stages, stage =>
        {
            Assert.NotEmpty(stage.Steps);
            Assert.All(stage.Steps, step => Assert.StartsWith($"{stage.Order}.", step.Code));
        });
    }

    [Fact]
    public async Task GetFlow_Step54_GoesBackOnlyTo49()
    {
        // ISDLC v12.0 flow (Oct 2026): "No" on 5.4 always returns to 4.9; the earlier 4.1 option was removed.
        var steps = AllSteps(await GetStagesAsync());
        var step = steps.Single(s => s.Code == "5.4");

        Assert.True(step.IsDecision);
        var jump = Assert.Single(step.NoJumps);
        Assert.Equal("4.9", jump.TargetStepCode);
        Assert.Equal("Akış 4.9 adımına dön", jump.Label);
    }

    [Fact]
    public async Task GetFlow_JumpTargetIdsMatchTheirCodes()
    {
        var steps = AllSteps(await GetStagesAsync());
        var idsByCode = steps.ToDictionary(s => s.Code, s => s.Id);
        var jumps = steps.SelectMany(s => s.NoJumps).ToList();

        Assert.Equal(10, jumps.Count);
        Assert.All(jumps, jump => Assert.Equal(idsByCode[jump.TargetStepCode], jump.TargetStepId));
    }

    [Fact]
    public async Task GetFlow_JumpsOnlyGoBackToEarlierSteps()
    {
        var steps = AllSteps(await GetStagesAsync());
        var ordersById = steps.ToDictionary(s => s.Id, s => s.Order);

        Assert.All(steps.Where(s => s.NoJumps.Count > 0), step =>
            Assert.All(step.NoJumps, jump => Assert.True(
                ordersById[jump.TargetStepId] < step.Order,
                $"{step.Code} jumps forward to {jump.TargetStepCode}")));
    }

    [Theory]
    [InlineData("3.2")]
    [InlineData("4.5")]
    [InlineData("5.1")]
    public async Task GetFlow_ApprovalSteps_AreApprovedBySoftwareDepartmentManager(string code)
    {
        var step = AllSteps(await GetStagesAsync()).Single(s => s.Code == code);

        Assert.Equal(UserRole.SoftwareDepartmentManager, step.ApproverRole);
    }

    [Fact]
    public async Task GetFlow_OnlyApprovalStepsHaveAnApprover()
    {
        var steps = AllSteps(await GetStagesAsync());

        Assert.Equal(["3.2", "4.5", "5.1"], steps.Where(s => s.ApproverRole is not null).Select(s => s.Code));
    }

    [Fact]
    public async Task GetFlow_Step51_IsPreparedBySalesAndApprovedByManager()
    {
        var step = AllSteps(await GetStagesAsync()).Single(s => s.Code == "5.1");

        Assert.Equal(UserRole.SalesRepresentative, step.ResponsibleRole);
        Assert.Equal(UserRole.SoftwareDepartmentManager, step.ApproverRole);
    }

    [Theory]
    [InlineData("1.2", ProjectStatus.Redirected)]
    [InlineData("3.2", ProjectStatus.Rejected)]
    [InlineData("5.3", ProjectStatus.Lost)]
    public async Task GetFlow_ClosingDecisions_HaveTheirOutcome(string code, ProjectStatus outcome)
    {
        var step = AllSteps(await GetStagesAsync()).Single(s => s.Code == code);

        Assert.True(step.IsDecision);
        Assert.Equal(outcome, step.NoOutcome);
    }

    [Fact]
    public async Task GetFlow_NoStepHasBothAnOutcomeAndJumps()
    {
        // data-model.md: a decision step never has NoOutcome and jump options together.
        var steps = AllSteps(await GetStagesAsync());

        Assert.DoesNotContain(steps, s => s.NoOutcome is not null && s.NoJumps.Count > 0);
    }

    [Fact]
    public async Task GetFlow_OnlyDecisionStepsHaveOutcomesOrJumps()
    {
        var steps = AllSteps(await GetStagesAsync());

        Assert.All(steps.Where(s => !s.IsDecision), step =>
        {
            Assert.Null(step.NoOutcome);
            Assert.Empty(step.NoJumps);
        });
    }
}
