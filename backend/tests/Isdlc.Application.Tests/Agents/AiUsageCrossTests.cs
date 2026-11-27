using System.Text;
using Isdlc.Application.Agents;
using Isdlc.Application.Agents.Dtos;
using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Tenants;
using Isdlc.Application.Tests.Common;
using Isdlc.Application.Tests.Identity;
using Isdlc.Domain.Agents;
using Isdlc.Domain.Identity;
using Isdlc.Domain.Tenants;
using Isdlc.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Agents;

/// <summary>
/// Cross tests for the company's AI model and usage (Faz 7 · A9, written by Baran for Dide's work; A10). They try what
/// AiUsageTests leaves out: the limit counts only this UTC month and only this company, it stops every assistant that
/// sends text but never the recording, and one company's model and usage never reach another. Each test opens its own
/// companies. Expectations: docs/configurability.md → Yapay zekâ modeli ve kullanımı.
/// </summary>
[Collection("Database")]
public class AiUsageCrossTests(DatabaseFixture database)
{
    private record Company(int TenantId, int AdminId);

    private async Task<Company> CreateCompanyAsync(string? model = null, int? limit = null)
    {
        int tenantId;
        await using (var scope = database.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();
            var tenant = new Tenant
            {
                Code = $"yz-{Guid.NewGuid():N}"[..20], Name = "Yapay Zekâ A.Ş.", CreatedAt = DateTime.UtcNow,
                AiEnabled = true, AiDataSharingAllowed = true, AiModel = model, AiMonthlyTokenLimit = limit,
            };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();
            tenantId = tenant.Id;
        }

        await using (var scope = database.CreateScope(tenantId))
        {
            await scope.ServiceProvider.GetRequiredService<ITenantTemplates>().ApplyAsync();
        }

        var admin = await TestUsers.CreateAsync(
            database, null, permission: Permission.Analysis, tenantId: tenantId, isTenantAdmin: true);
        await TestUsers.CreateAsync(database, null, permission: Permission.ProjectManagement, tenantId: tenantId);
        return new Company(tenantId, admin.Id);
    }

    private async Task<T> AgentsAsync<T>(Company company, FakeAgentClient agent, Func<AgentService, Task<T>> action)
    {
        await using var scope = database.CreateScope(company.TenantId);
        var service = new AgentService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
            new TestUser(company.AdminId, company.TenantId, isTenantAdmin: true),
            agent);
        return await action(service);
    }

    /// <summary>A usage row written straight to the company, e.g. one from an earlier month.</summary>
    private async Task AddUsageAsync(Company company, int tokens, DateTime createdAt)
    {
        await using var scope = database.CreateScope(company.TenantId);
        var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();
        db.AiUsages.Add(new AiUsage
        {
            UserId = company.AdminId, Assistant = AiAssistant.MeetingSuggestions, Model = "gemini-test-flash",
            InputTokens = tokens, OutputTokens = 0, DurationMs = 10, Succeeded = true, CreatedAt = createdAt,
        });
        await db.SaveChangesAsync();
    }

    private static SuggestMeetingItemsDto Notes() =>
        new() { MeetingDate = new DateOnly(2026, 11, 27), Notes = "Ekran taslaklarını Can hazırlayacak." };

    private static FakeAgentClient Answering(int input, int output) => new()
    {
        Result = new("Özet", [], 0, "gemini-test-flash", Usage: new AgentUsage("gemini-test-flash", input, output, null)),
    };

    // ---- The limit: this UTC month, this company ----

    [Fact]
    public async Task Limit_LastMonthsUsage_DoesNotCount()
    {
        var company = await CreateCompanyAsync(limit: 1000);
        var projectId = await TestProjects.CreateAsync(database, "6", tenantId: company.TenantId);
        var thisMonth = AiUsageRules.MonthOf(DateTime.UtcNow);
        await AddUsageAsync(company, 50_000, thisMonth.Start.AddSeconds(-1));

        var agent = new FakeAgentClient();
        await AgentsAsync(company, agent, s => s.SuggestMeetingItemsAsync(projectId, Notes(), CancellationToken.None));

        Assert.True(agent.WasCalled);
    }

    [Fact]
    public async Task Limit_AnotherCompanysUsage_DoesNotCount()
    {
        var mine = await CreateCompanyAsync(limit: 1000);
        var other = await CreateCompanyAsync();
        await AddUsageAsync(other, 50_000, DateTime.UtcNow);
        var projectId = await TestProjects.CreateAsync(database, "6", tenantId: mine.TenantId);

        var agent = new FakeAgentClient();
        await AgentsAsync(mine, agent, s => s.SuggestMeetingItemsAsync(projectId, Notes(), CancellationToken.None));

        Assert.True(agent.WasCalled);
    }

    [Fact]
    public async Task Limit_Reached_StopsEveryAssistantThatSendsText()
    {
        var company = await CreateCompanyAsync(limit: 1000);
        var projectId = await TestProjects.CreateAsync(database, "6", tenantId: company.TenantId);
        await AddUsageAsync(company, 1000, DateTime.UtcNow);
        var agent = new FakeAgentClient();

        var errors = new[]
        {
            await Assert.ThrowsAsync<ServiceUnavailableException>(() => AgentsAsync(company, agent,
                s => s.SuggestRisksAsync(projectId, new SuggestRisksDto { SourceText = "Notlar." }, CancellationToken.None))),
            await Assert.ThrowsAsync<ServiceUnavailableException>(() => AgentsAsync(company, agent,
                s => s.DraftRequestFromEmailAsync(new DraftRequestFromEmailDto { EmailText = "Talep." }, CancellationToken.None))),
        };

        Assert.All(errors, e => Assert.Equal(AiUsageRules.LimitReachedMessage, e.Message));
        Assert.False(agent.WasCalled);
    }

    [Fact]
    public async Task Limit_Reached_DoesNotStopTheRecording()
    {
        // A recording sends no text and costs no tokens, so the token limit does not apply to it.
        var company = await CreateCompanyAsync(limit: 1000);
        var projectId = await TestProjects.CreateAsync(database, "6", tenantId: company.TenantId);
        await AddUsageAsync(company, 5000, DateTime.UtcNow);
        var bytes = Encoding.UTF8.GetBytes("ses");

        var transcript = await AgentsAsync(company, new FakeAgentClient(), s =>
            s.TranscribeMeetingAsync(projectId, new MemoryStream(bytes), bytes.Length, CancellationToken.None));

        Assert.NotEmpty(transcript.Text);
    }

    // ---- One company's model and usage stay its own ----

    [Fact]
    public async Task Model_EachCompanySendsItsOwn()
    {
        var pro = await CreateCompanyAsync(model: "gemini-test-pro");
        var plain = await CreateCompanyAsync();
        var proProject = await TestProjects.CreateAsync(database, "6", tenantId: pro.TenantId);
        var plainProject = await TestProjects.CreateAsync(database, "6", tenantId: plain.TenantId);
        var proAgent = new FakeAgentClient();
        var plainAgent = new FakeAgentClient();

        await AgentsAsync(pro, proAgent, s => s.SuggestMeetingItemsAsync(proProject, Notes(), CancellationToken.None));
        await AgentsAsync(plain, plainAgent, s => s.SuggestMeetingItemsAsync(plainProject, Notes(), CancellationToken.None));

        Assert.Equal("gemini-test-pro", proAgent.LastRequest!.Model);
        Assert.Null(plainAgent.LastRequest!.Model);
    }

    [Fact]
    public async Task Usage_IsWrittenToTheCallersCompany_AndHiddenFromOthers()
    {
        var company = await CreateCompanyAsync();
        var projectId = await TestProjects.CreateAsync(database, "6", tenantId: company.TenantId);

        await AgentsAsync(company, Answering(30, 20), s => s.SuggestMeetingItemsAsync(projectId, Notes(), CancellationToken.None));

        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        // tenant: the test reads every company's rows to see where the one row went
        var row = await db.AiUsages.IgnoreQueryFilters().AsNoTracking().SingleAsync(u => u.ProjectId == projectId);
        Assert.Equal((company.TenantId, company.AdminId, 50), (row.TenantId, row.UserId, row.InputTokens + row.OutputTokens));
        Assert.False(await db.AiUsages.AnyAsync(u => u.ProjectId == projectId)); // Ithinka's scope does not see it
    }
}
