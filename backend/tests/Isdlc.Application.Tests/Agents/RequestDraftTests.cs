using Isdlc.Application.Agents;
using Isdlc.Application.Agents.Dtos;
using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Tests.Common;
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
}
