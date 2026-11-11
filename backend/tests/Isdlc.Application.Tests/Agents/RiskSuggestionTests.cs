using Isdlc.Application.Agents;
using Isdlc.Application.Agents.Dtos;
using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Tests.Common;
using Isdlc.Domain.Projects;
using Isdlc.Domain.Risks;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Agents;

/// <summary>
/// The .NET side of the risk assistant, with the AI service replaced by FakeAgentClient (no model is called).
/// Expectations: docs/risks-api.md → "Yapay zekâ önerileri".
/// </summary>
[Collection("Database")]
public class RiskSuggestionTests(DatabaseFixture database)
{
    private static readonly SuggestRisksDto Text = new() { SourceText = "ERP test ortamı henüz yok." };
    private static readonly SuggestRiskAssessmentDto TypedRisk = new() { Title = "Lisans gecikebilir" };

    private async Task<T> AsAsync<T>(string? email, FakeAgentClient agent, Func<IAgentService, Task<T>> action)
    {
        var userId = email is null ? (int?)null : await TestProjects.UserIdAsync(database, email);
        await using var scope = database.CreateScope();
        var service = new AgentService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(), new TestUser(userId), agent);
        return await action(service);
    }

    private Task<RiskSuggestionsDto> SuggestAsync(FakeAgentClient agent, int projectId, SuggestRisksDto? request = null) =>
        AsAsync(TestProjects.Analyst, agent, s => s.SuggestRisksAsync(projectId, request ?? Text, CancellationToken.None));

    private Task<RiskAssessmentSuggestionDto> AssessAsync(
        FakeAgentClient agent, int projectId, SuggestRiskAssessmentDto? request = null) =>
        AsAsync(TestProjects.Analyst, agent,
            s => s.SuggestRiskAssessmentAsync(projectId, request ?? TypedRisk, CancellationToken.None));

    private static FakeAgentClient Answering(int invalidCount = 0, params RiskAgentItem[] items) =>
        new() { RiskResult = new RiskAgentResult(items, invalidCount, "gemini-test") };

    private static RiskAgentItem Risk(
        string title = "ERP test ortamı geç gelirse entegrasyon testleri gecikir.",
        string? category = "Technical",
        int? probability = 4,
        int? impact = 4,
        params string[] warnings) =>
        new(title, "  ", category, probability, impact, "Erişim tarihi belirsiz.", "Yazılı termin alınır.",
            "ERP test ortamı henüz yok.", warnings);

    // ---- Checks before the AI service is called ----

    [Fact]
    public async Task SuggestRisks_NotSignedIn_ThrowsForbiddenAndDoesNotCallTheService()
    {
        var projectId = await TestProjects.CreateAsync(database, "8");
        var agent = Answering();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            AsAsync(null, agent, s => s.SuggestRisksAsync(projectId, Text, CancellationToken.None)));

        Assert.False(agent.WasCalled);
    }

    [Fact]
    public async Task SuggestRisks_ProjectMissing_ThrowsNotFound()
    {
        var agent = Answering();

        await Assert.ThrowsAsync<NotFoundException>(() => SuggestAsync(agent, int.MaxValue));

        Assert.False(agent.WasCalled);
    }

    [Fact]
    public async Task SuggestRisks_ProjectNotActive_ThrowsConflict()
    {
        var projectId = await TestProjects.CreateAsync(database, null, ProjectStatus.Rejected);
        var agent = Answering();

        await Assert.ThrowsAsync<ConflictException>(() => SuggestAsync(agent, projectId));

        Assert.False(agent.WasCalled);
    }

    [Fact]
    public async Task SuggestRisks_ServiceNotConfigured_ThrowsServiceUnavailable()
    {
        var projectId = await TestProjects.CreateAsync(database, "8");
        var agent = new FakeAgentClient { IsConfigured = false };

        var error = await Assert.ThrowsAsync<ServiceUnavailableException>(() => SuggestAsync(agent, projectId));

        Assert.Equal("Yapay zekâ servisi bu ortamda açık değil.", error.Message);
        Assert.False(agent.WasCalled);
    }

    [Fact]
    public async Task SuggestRisks_ServiceFails_PassesTheErrorOn()
    {
        var projectId = await TestProjects.CreateAsync(database, "8");
        var agent = new FakeAgentClient { Failure = new ServiceUnavailableException("Kota doldu.") };

        var error = await Assert.ThrowsAsync<ServiceUnavailableException>(() => SuggestAsync(agent, projectId));

        Assert.Equal("Kota doldu.", error.Message);
    }

    // ---- What the AI service is sent ----

    [Fact]
    public async Task SuggestRisks_SendsOnlyProjectCodeTrimmedTextAndCurrentStep()
    {
        var projectId = await TestProjects.CreateAsync(database, "8");
        var project = await TestProjects.GetAsync(database, projectId);
        var agent = Answering();

        await SuggestAsync(agent, projectId, new SuggestRisksDto { SourceText = "  Notlar.  " });

        var sent = Assert.IsType<RiskAgentRequest>(agent.LastRiskRequest);
        Assert.Equal(project.Code, sent.ProjectCode);
        Assert.Equal("Notlar.", sent.SourceText);
        Assert.StartsWith("8 ", sent.CurrentStep);
    }

    // ---- How the answer is checked ----

    [Fact]
    public async Task SuggestRisks_ValidRisk_GetsScoreSeverityAndCategory()
    {
        var projectId = await TestProjects.CreateAsync(database, "8");

        var result = await SuggestAsync(Answering(0, Risk()), projectId);

        var risk = Assert.Single(result.Items);
        Assert.Equal(RiskCategory.Technical, risk.Category);
        Assert.Equal((4, 4, 16), (risk.Probability, risk.Impact, risk.Score));
        Assert.Equal(RiskSeverity.High, risk.Severity);
        Assert.Null(risk.Description); // blank text counts as not given
        Assert.Equal("Yazılı termin alınır.", risk.Mitigation);
        Assert.Equal("ERP test ortamı henüz yok.", risk.Evidence);
        Assert.Empty(risk.Warnings);
        Assert.Equal("gemini-test", result.ModelName);
    }

    [Fact]
    public async Task SuggestRisks_UnknownCategory_KeepsTheRiskWithoutCategory()
    {
        var projectId = await TestProjects.CreateAsync(database, "8");

        var result = await SuggestAsync(Answering(0, Risk(category: "Weather"), Risk(category: null)), projectId);

        Assert.All(result.Items, risk => Assert.Null(risk.Category));
    }

    [Theory]
    [InlineData(6, 3)]
    [InlineData(0, 3)]
    [InlineData(3, null)]
    public async Task SuggestRisks_InvalidOrHalfScore_DropsTheScoreAndWarns(int? probability, int? impact)
    {
        var projectId = await TestProjects.CreateAsync(database, "8");

        var result = await SuggestAsync(Answering(0, Risk(probability: probability, impact: impact)), projectId);

        var risk = Assert.Single(result.Items);
        Assert.Null(risk.Probability);
        Assert.Null(risk.Impact);
        Assert.Null(risk.Score);
        Assert.Null(risk.Severity);
        Assert.Contains("Önerilen puan geçersizdi; olasılık ve etkiyi siz seçin.", risk.Warnings);
    }

    [Fact]
    public async Task SuggestRisks_UnscoredRisk_KeepsTheAgentsWarningOnly()
    {
        var projectId = await TestProjects.CreateAsync(database, "8");

        var result = await SuggestAsync(
            Answering(0, Risk(probability: null, impact: null, warnings: "Model bu riski puanlamadı.")), projectId);

        var risk = Assert.Single(result.Items);
        Assert.Null(risk.Score);
        Assert.Equal(["Model bu riski puanlamadı."], risk.Warnings);
    }

    [Fact]
    public async Task SuggestRisks_BlankOrTooLongTitle_IsSkippedAndCounted()
    {
        var projectId = await TestProjects.CreateAsync(database, "8");

        var result = await SuggestAsync(
            Answering(1, Risk(title: "  "), Risk(title: new string('a', 301)), Risk(title: "  Geçerli risk  ")),
            projectId);

        Assert.Equal("Geçerli risk", Assert.Single(result.Items).Title);
        Assert.Equal(3, result.SkippedCount);
    }

    // ---- Assessment of a typed risk ----

    [Fact]
    public async Task SuggestRiskAssessment_SendsTitleDescriptionAndCurrentStep()
    {
        var projectId = await TestProjects.CreateAsync(database, "8");
        var project = await TestProjects.GetAsync(database, projectId);
        var agent = new FakeAgentClient();

        await AssessAsync(agent, projectId,
            new SuggestRiskAssessmentDto { Title = "  Lisans gecikebilir ", Description = "   " });

        var sent = Assert.IsType<RiskAssessmentAgentRequest>(agent.LastRiskAssessmentRequest);
        Assert.Equal(project.Code, sent.ProjectCode);
        Assert.Equal("Lisans gecikebilir", sent.Title);
        Assert.Null(sent.Description);
        Assert.StartsWith("8 ", sent.CurrentStep);
    }

    [Fact]
    public async Task SuggestRiskAssessment_ReturnsScoreSeverityAndMitigation()
    {
        var projectId = await TestProjects.CreateAsync(database, "8");

        var result = await AssessAsync(new FakeAgentClient(), projectId);

        Assert.Equal((3, 4, 12), (result.Probability, result.Impact, result.Score));
        Assert.Equal(RiskSeverity.Medium, result.Severity);
        Assert.Equal("Gerekçe.", result.AssessmentReason);
        Assert.Equal("Önleyici aksiyon.", result.Mitigation);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task SuggestRiskAssessment_InvalidScore_DropsItAndWarns()
    {
        var projectId = await TestProjects.CreateAsync(database, "8");
        var agent = new FakeAgentClient
        {
            RiskAssessmentResult = new RiskAssessmentAgentResult(9, 2, null, "Aksiyon.", [], "test-model"),
        };

        var result = await AssessAsync(agent, projectId);

        Assert.Null(result.Score);
        Assert.Equal("Aksiyon.", result.Mitigation);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public async Task SuggestRiskAssessment_ProjectNotActive_ThrowsConflict()
    {
        var projectId = await TestProjects.CreateAsync(database, null, ProjectStatus.Closed);
        var agent = new FakeAgentClient();

        await Assert.ThrowsAsync<ConflictException>(() => AssessAsync(agent, projectId));

        Assert.False(agent.WasCalled);
    }
}
