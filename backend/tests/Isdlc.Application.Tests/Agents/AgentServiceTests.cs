using Isdlc.Application.Agents;
using Isdlc.Application.Agents.Dtos;
using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Tests.Common;
using Isdlc.Application.Tests.Identity;
using Isdlc.Domain.Meetings;
using Isdlc.Domain.OpenIssues;
using Isdlc.Domain.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Agents;

/// <summary>
/// The .NET side of the meeting assistant, with the AI service replaced by FakeAgentClient (no model is called).
/// Expectations: docs/open-issues-meetings-api.md → "Yapay zekâ önerileri" and docs/ai-vision.md.
/// Cross test (PLANNING.md): Dide → Erva's modules.
/// </summary>
[Collection("Database")]
public class AgentServiceTests(DatabaseFixture database)
{
    private static readonly DateOnly MeetingDate = new(2026, 11, 5);

    private static SuggestMeetingItemsDto Notes(string notes = "Ekran taslaklarını Can Öztürk hazırlayacak.") =>
        new() { MeetingDate = MeetingDate, Notes = notes };

    private async Task<T> AsAsync<T>(string? email, FakeAgentClient agent, Func<IAgentService, Task<T>> action)
    {
        var userId = email is null ? (int?)null : await TestProjects.UserIdAsync(database, email);
        await using var scope = database.CreateScope();
        var service = new AgentService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(), new TestUser(userId), agent);
        return await action(service);
    }

    private Task<MeetingSuggestionsDto> SuggestAsync(FakeAgentClient agent, int projectId, SuggestMeetingItemsDto? request = null) =>
        AsAsync(TestProjects.Analyst, agent, s => s.SuggestMeetingItemsAsync(projectId, request ?? Notes(), CancellationToken.None));

    private static FakeAgentClient Answering(params MeetingAgentItem[] items) =>
        new() { Result = new MeetingAgentResult("ERP ve raporlar konuşuldu.", items, 0, "gemini-test") };

    private static MeetingAgentItem Item(
        string type, string title, string? ownerName = null, DateOnly? dueDate = null, params string[] warnings) =>
        new(type, title, null, ownerName, dueDate, $"Dayanak: {title}", warnings);

    // ---- Status ----

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetStatus_FollowsTheService(bool available)
    {
        var status = await AsAsync(TestProjects.Sales, new FakeAgentClient { IsAvailable = available },
            s => s.GetStatusAsync(CancellationToken.None));

        Assert.Equal(available, status.MeetingAssistantEnabled);
        Assert.Equal(available, status.RiskAssistantEnabled);
    }

    // ---- Checks before the AI service is called ----

    [Fact]
    public async Task Suggest_NotSignedIn_ThrowsForbiddenAndDoesNotCallTheService()
    {
        var projectId = await TestProjects.CreateAsync(database, "7.1");
        var agent = Answering();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            AsAsync(null, agent, s => s.SuggestMeetingItemsAsync(projectId, Notes(), CancellationToken.None)));

        Assert.Null(agent.LastRequest);
    }

    [Fact]
    public async Task Suggest_ProjectMissing_ThrowsNotFoundAndDoesNotCallTheService()
    {
        var agent = Answering();

        await Assert.ThrowsAsync<NotFoundException>(() => SuggestAsync(agent, int.MaxValue));

        Assert.Null(agent.LastRequest);
    }

    [Fact]
    public async Task Suggest_ProjectNotActive_ThrowsConflictAndDoesNotCallTheService()
    {
        var projectId = await TestProjects.CreateAsync(database, null, ProjectStatus.Closed);
        var agent = Answering();

        await Assert.ThrowsAsync<ConflictException>(() => SuggestAsync(agent, projectId));

        Assert.Null(agent.LastRequest);
    }

    [Fact]
    public async Task Suggest_ServiceNotConfigured_ThrowsServiceUnavailable()
    {
        var projectId = await TestProjects.CreateAsync(database, "7.1");
        var agent = new FakeAgentClient { IsConfigured = false };

        var error = await Assert.ThrowsAsync<ServiceUnavailableException>(() => SuggestAsync(agent, projectId));

        Assert.Equal("Yapay zekâ servisi bu ortamda açık değil.", error.Message);
        Assert.Null(agent.LastRequest);
    }

    [Fact]
    public async Task Suggest_ServiceFails_PassesTheErrorOn()
    {
        var projectId = await TestProjects.CreateAsync(database, "7.1");
        var agent = new FakeAgentClient { Failure = new ServiceUnavailableException("Yapay zekâ servisi cevap vermedi.") };

        var error = await Assert.ThrowsAsync<ServiceUnavailableException>(() => SuggestAsync(agent, projectId));

        Assert.Equal("Yapay zekâ servisi cevap vermedi.", error.Message);
    }

    // ---- What the AI service is sent ----

    [Fact]
    public async Task Suggest_SendsOnlyProjectCodeDateTrimmedNotesAndCurrentStep()
    {
        var projectId = await TestProjects.CreateAsync(database, "7.1");
        var project = await TestProjects.GetAsync(database, projectId);
        var agent = Answering();

        await SuggestAsync(agent, projectId, Notes("  Toplantı notları.  "));

        var sent = Assert.IsType<MeetingAgentRequest>(agent.LastRequest);
        Assert.Equal(project.Code, sent.ProjectCode);
        Assert.Equal(MeetingDate, sent.MeetingDate);
        Assert.Equal("Toplantı notları.", sent.Notes);
        Assert.StartsWith("7.1 ", sent.CurrentStep);
    }

    // ---- How the answer is split ----

    [Fact]
    public async Task Suggest_SplitsDecisionsActionsAndOthers()
    {
        var projectId = await TestProjects.CreateAsync(database, "7.1");
        var agent = Answering(
            Item("Decision", "  Raporlar PDF olarak da alınacak  ", ownerName: "Mert Kaya"),
            Item("Action", "Ekran taslakları", ownerName: "Müşteri BT", dueDate: MeetingDate.AddDays(7), "Termin belirsiz"),
            Item("Question", "Fotoğraflar kaç gün saklanacak?", ownerName: "  Saha Operasyon  "),
            Item("Dependency", "ERP test ortamı"));

        var result = await SuggestAsync(agent, projectId);

        Assert.Equal("ERP ve raporlar konuşuldu.", result.Summary);
        Assert.Equal("gemini-test", result.ModelName);

        // A decision is saved owned by whoever saves the minutes, so only its title and evidence are kept.
        var decision = Assert.Single(result.Decisions);
        Assert.Equal("Raporlar PDF olarak da alınacak", decision.Title);

        var action = Assert.Single(result.Actions);
        Assert.Equal("Ekran taslakları", action.Title);
        Assert.Equal(MeetingDate.AddDays(7), action.DueDate);
        Assert.Equal(["Termin belirsiz"], action.Warnings);
        Assert.Equal("Dayanak: Ekran taslakları", action.Evidence);

        Assert.Equal([OpenIssueType.Question, OpenIssueType.Dependency], result.Others.Select(x => x.Type));
        Assert.Equal("Saha Operasyon", result.Others[0].OwnerName);
        Assert.Null(result.Others[1].OwnerName);
    }

    [Fact]
    public async Task Suggest_UnknownTypeOrBlankTitle_IsSkippedAndCounted()
    {
        var projectId = await TestProjects.CreateAsync(database, "7.1");
        var agent = new FakeAgentClient
        {
            // The service already dropped 2 items it could not read.
            Result = new MeetingAgentResult("Özet", [
                Item("Action", "Geçerli aksiyon"),
                Item("Toplantı", "Bilinmeyen tür"),
                Item("Decision", "   "),
            ], InvalidCount: 2, "gemini-test"),
        };

        var result = await SuggestAsync(agent, projectId);

        Assert.Single(result.Actions);
        Assert.Empty(result.Decisions);
        Assert.Equal(4, result.SkippedCount);
    }

    // ---- Owner names (actions) ----

    [Theory]
    [InlineData("Can Öztürk")]
    [InlineData("can öztürk")]
    [InlineData("CAN ÖZTÜRK")]
    [InlineData("  Can Öztürk  ")]
    public async Task Suggest_OwnerIsAnActiveUser_SetsOwnerUserId(string ownerName)
    {
        var projectId = await TestProjects.CreateAsync(database, "7.1");
        var developerId = await TestProjects.UserIdAsync(database, TestProjects.Developer);

        var result = await SuggestAsync(Answering(Item("Action", "Ekran taslakları", ownerName)), projectId);

        var action = Assert.Single(result.Actions);
        Assert.Equal(developerId, action.OwnerUserId);
        Assert.Null(action.OwnerName);
    }

    [Fact]
    public async Task Suggest_OwnerNameWithTurkishDottedCapitalI_FindsTheUser()
    {
        var projectId = await TestProjects.CreateAsync(database, "7.1");
        var analystId = await TestProjects.UserIdAsync(database, TestProjects.Analyst);

        // "ELİF" is the Turkish capital of "elif" (İ, not I).
        var result = await SuggestAsync(Answering(Item("Action", "Analiz dokümanı", "ELİF YILMAZ")), projectId);

        Assert.Equal(analystId, Assert.Single(result.Actions).OwnerUserId);
    }

    [Theory]
    [InlineData("Müşteri BT", "Müşteri BT")]
    [InlineData("  Depo Müdürü  ", "Depo Müdürü")]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    public async Task Suggest_OwnerIsNotAUser_KeepsTheName(string? ownerName, string? expected)
    {
        var projectId = await TestProjects.CreateAsync(database, "7.1");

        var result = await SuggestAsync(Answering(Item("Action", "Aksiyon", ownerName)), projectId);

        var action = Assert.Single(result.Actions);
        Assert.Null(action.OwnerUserId);
        Assert.Equal(expected, action.OwnerName);
    }

    [Fact]
    public async Task Suggest_OwnerIsAnInactiveUser_IsNotMatched()
    {
        var projectId = await TestProjects.CreateAsync(database, "7.1");
        var name = $"Pasif Kişi {Guid.NewGuid():N}"[..20];
        await TestUsers.CreateAsync(database, password: null, isActive: false, fullName: name);

        var result = await SuggestAsync(Answering(Item("Action", "Aksiyon", name)), projectId);

        var action = Assert.Single(result.Actions);
        Assert.Null(action.OwnerUserId);
        Assert.Equal(name, action.OwnerName);
    }

    [Fact]
    public async Task Suggest_TwoActiveUsersWithTheSameName_LeavesTheChoiceToThePerson()
    {
        var projectId = await TestProjects.CreateAsync(database, "7.1");
        var name = $"Aynı Ad {Guid.NewGuid():N}"[..18];
        await TestUsers.CreateAsync(database, password: null, fullName: name);
        await TestUsers.CreateAsync(database, password: null, fullName: name);

        var result = await SuggestAsync(Answering(Item("Action", "Aksiyon", name)), projectId);

        var action = Assert.Single(result.Actions);
        Assert.Null(action.OwnerUserId);
        Assert.Equal(name, action.OwnerName);
    }

    // ---- Minutes draft (type, participants, agenda) ----

    private static FakeAgentClient WithMinutes(MeetingAgentMinutes? minutes) =>
        new() { Result = new MeetingAgentResult("Özet", [], 0, "gemini-test", minutes) };

    private static MeetingAgentMinutes Minutes(
        string type = "AnalysisSession", string participants = "Elif Yılmaz, Can Öztürk", string agenda = "- Ekranlar") =>
        new(type, " Ekranlar konuşulmuş. ", participants, agenda, ["Katılımcı notlarda geçmiyor: Müşteri BT"]);

    [Fact]
    public async Task Suggest_MinutesDraft_IsPassedOnWithTheMeetingType()
    {
        var projectId = await TestProjects.CreateAsync(database, "7.1");

        var result = await SuggestAsync(WithMinutes(Minutes(participants: "  Elif Yılmaz, Can Öztürk  ")), projectId);

        var minutes = Assert.IsType<MinutesDraftDto>(result.Minutes);
        Assert.Equal(MeetingType.AnalysisSession, minutes.Type);
        Assert.Equal("Ekranlar konuşulmuş.", minutes.TypeReason);
        Assert.Equal("Elif Yılmaz, Can Öztürk", minutes.Participants);
        Assert.Equal("- Ekranlar", minutes.Agenda);
        Assert.Equal(["Katılımcı notlarda geçmiyor: Müşteri BT"], minutes.Warnings);
    }

    [Fact]
    public async Task Suggest_NoMinutesDraft_ReturnsNull()
    {
        var projectId = await TestProjects.CreateAsync(database, "7.1");

        var result = await SuggestAsync(WithMinutes(null), projectId);

        Assert.Null(result.Minutes);
    }

    [Theory]
    [InlineData("Party", "Elif Yılmaz", "- Konu")]
    [InlineData("42", "Elif Yılmaz", "- Konu")]
    [InlineData("Review", "  ", "- Konu")]
    [InlineData("Review", "Elif Yılmaz", "")]
    public async Task Suggest_MinutesDraftTheFormWouldReject_IsDropped(string type, string participants, string agenda)
    {
        var projectId = await TestProjects.CreateAsync(database, "7.1");

        var result = await SuggestAsync(WithMinutes(Minutes(type, participants, agenda)), projectId);

        Assert.Null(result.Minutes);
    }

    [Fact]
    public async Task Suggest_MinutesDraftLongerThanTheFormAllows_IsDropped()
    {
        var projectId = await TestProjects.CreateAsync(database, "7.1");

        var tooManyPeople = await SuggestAsync(WithMinutes(Minutes(participants: new string('a', 1001))), projectId);
        var tooLongAgenda = await SuggestAsync(WithMinutes(Minutes(agenda: new string('a', 4001))), projectId);

        Assert.Null(tooManyPeople.Minutes);
        Assert.Null(tooLongAgenda.Minutes);
    }
}
