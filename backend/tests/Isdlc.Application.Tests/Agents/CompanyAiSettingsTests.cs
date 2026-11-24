using System.Text;
using Isdlc.Application.Agents;
using Isdlc.Application.Agents.Dtos;
using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Tests.Common;
using Isdlc.Application.Tests.Identity;
using Isdlc.Domain.Identity;
using Isdlc.Domain.Tenants;
using Isdlc.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Agents;

/// <summary>
/// A company's AI switches on a real database (Faz 6 · T6): the status the screens read, and the assistants refusing
/// before the AI service is called. Each test opens its own company with the switches it needs, the templates and an
/// analyst and a project manager, so Ithinka and Deneme are never changed.
/// Expectations: docs/multi-tenancy.md → "Yapay zekâ ayarlarının etkisi".
/// </summary>
[Collection("Database")]
public class CompanyAiSettingsTests(DatabaseFixture database)
{
    private record Company(int TenantId, int AnalystId);

    private async Task<Company> CreateCompanyAsync(bool aiEnabled, bool dataSharing)
    {
        int tenantId;
        await using (var scope = database.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();
            var tenant = new Tenant
            {
                Code = $"yz-{Guid.NewGuid():N}"[..20], Name = "Yapay Zekâ A.Ş.", CreatedAt = DateTime.UtcNow,
                AiEnabled = aiEnabled, AiDataSharingAllowed = dataSharing,
            };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();
            tenantId = tenant.Id;
        }

        await using (var scope = database.CreateScope(tenantId))
        {
            await scope.ServiceProvider.GetRequiredService<ITenantTemplates>().ApplyAsync();
        }

        var analyst = await TestUsers.CreateAsync(database, null, role: UserRole.BusinessAnalyst, tenantId: tenantId);
        await TestUsers.CreateAsync(database, null, role: UserRole.ProjectManager, tenantId: tenantId);
        return new Company(tenantId, analyst.Id);
    }

    private async Task<T> AsCompanyAsync<T>(Company company, FakeAgentClient agent, Func<AgentService, Task<T>> action)
    {
        await using var scope = database.CreateScope(company.TenantId);
        var service = new AgentService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
            new TestUser(company.AnalystId, company.TenantId),
            agent);
        return await action(service);
    }

    private static SuggestMeetingItemsDto Notes() =>
        new() { MeetingDate = new DateOnly(2026, 11, 24), Notes = "Ekran taslaklarını Can hazırlayacak." };

    // ---- Status ----

    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, false, true)]
    [InlineData(true, true, true, true)]
    public async Task GetStatus_FollowsTheCompanysSwitches(bool aiEnabled, bool dataSharing, bool textAssistants, bool transcription)
    {
        var company = await CreateCompanyAsync(aiEnabled, dataSharing);

        var status = await AsCompanyAsync(company, new FakeAgentClient(), s => s.GetStatusAsync(CancellationToken.None));

        Assert.Equal(new AgentStatusDto(textAssistants, textAssistants, textAssistants, transcription), status);
    }

    [Fact]
    public async Task GetStatus_ServiceDown_EverythingOffEvenWithBothSwitchesOn()
    {
        var company = await CreateCompanyAsync(aiEnabled: true, dataSharing: true);

        var status = await AsCompanyAsync(company, new FakeAgentClient { IsAvailable = false }, s => s.GetStatusAsync(CancellationToken.None));

        Assert.Equal(new AgentStatusDto(false, false, false, false), status);
    }

    [Fact]
    public async Task GetStatus_NobodySignedIn_EverythingOff()
    {
        await using var scope = database.CreateScope();
        var service = new AgentService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(), new TestUser(null), new FakeAgentClient());

        Assert.Equal(new AgentStatusDto(false, false, false, false), await service.GetStatusAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GetStatus_NewCompanyDefaults_EverythingOff()
    {
        // Deneme was opened with the defaults: AI off until its manager switches it on.
        var analystId = await TestProjects.UserIdAsync(database, TestTenants.DenemeAnalyst, database.DenemeId);
        await using var scope = database.CreateScope(database.DenemeId);
        var service = new AgentService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
            new TestUser(analystId, database.DenemeId), new FakeAgentClient());

        Assert.Equal(new AgentStatusDto(false, false, false, false), await service.GetStatusAsync(CancellationToken.None));
    }

    // ---- Assistants that send text ----

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public async Task TextAssistants_NotAllowed_Are503AndTheServiceIsNotCalled(bool aiEnabled, bool dataSharing)
    {
        var company = await CreateCompanyAsync(aiEnabled, dataSharing);
        var projectId = await TestProjects.CreateAsync(database, "6", tenantId: company.TenantId);
        var agent = new FakeAgentClient();

        var meeting = await Assert.ThrowsAsync<ServiceUnavailableException>(() => AsCompanyAsync(company, agent,
            s => s.SuggestMeetingItemsAsync(projectId, Notes(), CancellationToken.None)));
        var risks = await Assert.ThrowsAsync<ServiceUnavailableException>(() => AsCompanyAsync(company, agent,
            s => s.SuggestRisksAsync(projectId, new SuggestRisksDto { SourceText = "Test ortamı yok." }, CancellationToken.None)));
        var draft = await Assert.ThrowsAsync<ServiceUnavailableException>(() => AsCompanyAsync(company, agent,
            s => s.DraftRequestFromEmailAsync(new DraftRequestFromEmailDto { EmailText = "Yeni ekran istiyoruz." }, CancellationToken.None)));

        Assert.All(new[] { meeting, risks, draft }, e => Assert.Equal(CompanyAiRules.ClosedMessage, e.Message));
        Assert.False(agent.WasCalled);
    }

    [Fact]
    public async Task TextAssistants_BothSwitchesOn_CallTheService()
    {
        var company = await CreateCompanyAsync(aiEnabled: true, dataSharing: true);
        var projectId = await TestProjects.CreateAsync(database, "6", tenantId: company.TenantId);
        var agent = new FakeAgentClient();

        await AsCompanyAsync(company, agent, s => s.SuggestMeetingItemsAsync(projectId, Notes(), CancellationToken.None));

        Assert.NotNull(agent.LastRequest);
    }

    // ---- Transcription ----

    [Fact]
    public async Task Transcribe_AiOnWithoutDataSharing_IsAllowed()
    {
        // The recording stays on the AI service's machine: the permission to send data out is not needed.
        var company = await CreateCompanyAsync(aiEnabled: true, dataSharing: false);
        var projectId = await TestProjects.CreateAsync(database, "6", tenantId: company.TenantId);
        var agent = new FakeAgentClient();

        var transcript = await AsCompanyAsync(company, agent, async s =>
        {
            await using var recording = new MemoryStream(Encoding.UTF8.GetBytes("fake audio"));
            return await s.TranscribeMeetingAsync(projectId, recording, recording.Length, CancellationToken.None);
        });

        Assert.False(string.IsNullOrEmpty(transcript.Text));
        Assert.NotNull(agent.LastRecording);
    }

    [Fact]
    public async Task Transcribe_AiOff_Is503AndTheServiceIsNotCalled()
    {
        var company = await CreateCompanyAsync(aiEnabled: false, dataSharing: false);
        var projectId = await TestProjects.CreateAsync(database, "6", tenantId: company.TenantId);
        var agent = new FakeAgentClient();

        var error = await Assert.ThrowsAsync<ServiceUnavailableException>(() => AsCompanyAsync(company, agent, async s =>
        {
            await using var recording = new MemoryStream(Encoding.UTF8.GetBytes("fake audio"));
            return await s.TranscribeMeetingAsync(projectId, recording, recording.Length, CancellationToken.None);
        }));

        Assert.Equal(CompanyAiRules.ClosedMessage, error.Message);
        Assert.Null(agent.LastRecording);
    }
}
