using Isdlc.Application.Agents;
using Isdlc.Application.Agents.Dtos;
using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Tests.Common;
using Isdlc.Application.Tests.Identity;
using Isdlc.Domain.Identity;
using Isdlc.Domain.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Agents;

/// <summary>
/// "E-postadan doldur" in the new request form, with the AI service replaced by FakeAgentClient (no model is called).
/// Expectations: docs/projects-api.md → "Yapay zekâ: e-postadan talep taslağı".
/// </summary>
[Collection("Database")]
public class RequestDraftTests(DatabaseFixture database)
{
    private const string Email = "  Kimden: Ayşe Kaya\nDepo stok takip ekranı istiyoruz.  ";

    private async Task<RequestDraftDto> DraftAsync(FakeAgentClient agent, string? email = TestProjects.Analyst)
    {
        var userId = email is null ? (int?)null : await TestProjects.UserIdAsync(database, email);
        await using var scope = database.CreateScope();
        var service = new AgentService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(), new TestUser(userId), agent);
        return await service.DraftRequestFromEmailAsync(new DraftRequestFromEmailDto { EmailText = Email }, CancellationToken.None);
    }

    private static RequestAgentDraft Draft(
        string? name = "Depo stok takip ekranı", string? requestType = "NewProject",
        DateOnly? requestDate = null, DateOnly? dueDate = null) =>
        new(name, "Stok durumu anlık izlenecek.", "Örnek Lojistik A.Ş.", "Ayşe Kaya", "ayse.kaya@ornek.com",
            requestType, requestDate, dueDate, null, ["İmzadan tahmin edildi."], "gemini-test");

    [Fact]
    public async Task Draft_Analyst_GetsTheFormAndOnlyTheTrimmedEmailIsSent()
    {
        var agent = new FakeAgentClient
        {
            RequestDraft = Draft(requestDate: new DateOnly(2026, 11, 18), dueDate: new DateOnly(2026, 12, 31)),
        };

        var draft = await DraftAsync(agent);

        Assert.Equal("Depo stok takip ekranı", draft.Name);
        Assert.Equal("ayse.kaya@ornek.com", draft.RequesterContact);
        Assert.Equal(RequestType.NewProject, draft.RequestType);
        Assert.Equal(new DateOnly(2026, 12, 31), draft.DueDate);
        Assert.Equal(["İmzadan tahmin edildi."], draft.Warnings);
        Assert.Equal("gemini-test", draft.ModelName);
        Assert.Equal(Email.Trim(), agent.LastRequestDraftRequest!.EmailText);
    }

    [Theory]
    [InlineData(TestProjects.Developer)]
    [InlineData(TestProjects.ProjectManager)]
    [InlineData(null)]
    public async Task Draft_NotAnAnalyst_IsForbiddenAndTheServiceIsNotCalled(string? email)
    {
        var agent = new FakeAgentClient();

        await Assert.ThrowsAsync<ForbiddenException>(() => DraftAsync(agent, email));

        Assert.False(agent.WasCalled);
    }

    [Fact]
    public async Task Draft_ServiceNotConfigured_Is503()
    {
        await Assert.ThrowsAsync<ServiceUnavailableException>(() => DraftAsync(new FakeAgentClient { IsConfigured = false }));
    }

    [Theory]
    [InlineData("Consulting")]
    [InlineData("7")]
    [InlineData(null)]
    public async Task Draft_UnknownRequestType_IsNull(string? requestType)
    {
        var draft = await DraftAsync(new FakeAgentClient { RequestDraft = Draft(requestType: requestType) });

        Assert.Null(draft.RequestType);
        Assert.Equal("Depo stok takip ekranı", draft.Name);
    }

    [Fact]
    public async Task Draft_ValuesTheFormWouldRefuse_AreDroppedOneByOne()
    {
        var agent = new FakeAgentClient
        {
            RequestDraft = Draft(
                name: new string('x', 201), requestDate: new DateOnly(2026, 11, 18), dueDate: new DateOnly(2026, 11, 1)),
        };

        var draft = await DraftAsync(agent);

        Assert.Null(draft.Name);
        Assert.Null(draft.DueDate);
        Assert.Equal(new DateOnly(2026, 11, 18), draft.RequestDate);
        Assert.Equal("Örnek Lojistik A.Ş.", draft.CustomerName);
    }

    // ---- Cross tests (PLANNING.md): Dide → Erva's modules ----

    private static RequestAgentDraft DraftWithTexts(
        string? name = "Depo stok takip ekranı", string? description = "Stok durumu anlık izlenecek.",
        string? customerName = "Örnek Lojistik A.Ş.", string? requesterName = "Ayşe Kaya",
        string? requesterContact = "ayse.kaya@ornek.com", string? notes = "Mobil erişim istendi.") =>
        new(name, description, customerName, requesterName, requesterContact, "NewProject", null, null, notes, [], "gemini-test");

    [Fact]
    public async Task Draft_InactiveAnalyst_IsForbiddenAndTheServiceIsNotCalled()
    {
        var user = await TestUsers.CreateAsync(database, password: null, isActive: false, permission: Permission.Analysis);
        var agent = new FakeAgentClient();
        await using var scope = database.CreateScope();
        var service = new AgentService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(), new TestUser(user.Id), agent);

        var error = await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.DraftRequestFromEmailAsync(new DraftRequestFromEmailDto { EmailText = Email }, CancellationToken.None));

        Assert.StartsWith("Talep açmak için «Analiz adımları» yetkisi gerekir. Şirket yöneticinizden isteyin: ", error.Message);
        Assert.Contains("Mert Kaya", error.Message);
        Assert.False(agent.WasCalled);
    }

    [Fact]
    public async Task Draft_TodayIsSentForRelativeDates()
    {
        var agent = new FakeAgentClient();
        var before = DateOnly.FromDateTime(DateTime.UtcNow);

        await DraftAsync(agent);

        var today = agent.LastRequestDraftRequest!.Today;
        Assert.InRange(today, before, DateOnly.FromDateTime(DateTime.UtcNow));
    }

    [Theory]
    [InlineData("NewProject", RequestType.NewProject)]
    [InlineData("AdditionalModule", RequestType.AdditionalModule)]
    [InlineData("CompanyProduct", RequestType.CompanyProduct)]
    [InlineData("Support", RequestType.Support)]
    public async Task Draft_EveryRequestTypeName_IsRead(string requestType, RequestType expected)
    {
        var draft = await DraftAsync(new FakeAgentClient { RequestDraft = Draft(requestType: requestType) });

        Assert.Equal(expected, draft.RequestType);
    }

    [Fact]
    public async Task Draft_RequestTypeInAnotherCase_IsNull()
    {
        var draft = await DraftAsync(new FakeAgentClient { RequestDraft = Draft(requestType: "newproject") });

        Assert.Null(draft.RequestType);
    }

    [Fact]
    public async Task Draft_Texts_AreTrimmedAndBlankOnesAreNull()
    {
        var agent = new FakeAgentClient
        {
            RequestDraft = DraftWithTexts(name: "  Depo stok takip ekranı  ", description: "   ", notes: ""),
        };

        var draft = await DraftAsync(agent);

        Assert.Equal("Depo stok takip ekranı", draft.Name);
        Assert.Null(draft.Description);
        Assert.Null(draft.Notes);
    }

    [Fact]
    public async Task Draft_TextsAtTheFormsLimit_AreKept()
    {
        var agent = new FakeAgentClient
        {
            RequestDraft = DraftWithTexts(
                name: new string('a', 200), description: new string('b', 2000), customerName: new string('c', 200),
                requesterName: new string('d', 200), requesterContact: new string('e', 200), notes: new string('f', 2000)),
        };

        var draft = await DraftAsync(agent);

        Assert.Equal(
            [200, 2000, 200, 200, 200, 2000],
            new[] { draft.Name, draft.Description, draft.CustomerName, draft.RequesterName, draft.RequesterContact, draft.Notes }
                .Select(t => t!.Length));
    }

    [Fact]
    public async Task Draft_TextsOverTheFormsLimit_AreEachDropped()
    {
        var agent = new FakeAgentClient
        {
            RequestDraft = DraftWithTexts(
                description: new string('b', 2001), requesterContact: new string('e', 201), notes: new string('f', 2001)),
        };

        var draft = await DraftAsync(agent);

        Assert.Null(draft.Description);
        Assert.Null(draft.RequesterContact);
        Assert.Null(draft.Notes);
        Assert.Equal("Depo stok takip ekranı", draft.Name);
        Assert.Equal("Ayşe Kaya", draft.RequesterName);
    }

    [Theory]
    [InlineData("2026-11-18", "2026-11-18", "2026-11-18")] // due on the request day: kept
    [InlineData(null, "2026-11-01", "2026-11-01")]          // no request date to compare with: kept
    [InlineData("2026-11-18", null, null)]
    public async Task Draft_DueDate_IsKeptUnlessBeforeTheRequestDate(string? requestDate, string? dueDate, string? expected)
    {
        var agent = new FakeAgentClient
        {
            RequestDraft = Draft(
                requestDate: requestDate is null ? null : DateOnly.Parse(requestDate),
                dueDate: dueDate is null ? null : DateOnly.Parse(dueDate)),
        };

        var draft = await DraftAsync(agent);

        Assert.Equal(expected is null ? null : DateOnly.Parse(expected), draft.DueDate);
    }
}
