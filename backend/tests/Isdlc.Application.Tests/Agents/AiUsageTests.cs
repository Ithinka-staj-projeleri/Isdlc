using System.Text;
using Isdlc.Application.Agents;
using Isdlc.Application.Agents.Dtos;
using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Tenants;
using Isdlc.Application.Tenants.Dtos;
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
/// The company's AI model and usage on a real database (Faz 7 · A9): the model sent to the service, every call written to
/// AiUsage, the monthly limit, the month's summary and the model choice in the settings. Each test opens its own company,
/// so the usage of other tests never adds up. Expectations: docs/configurability.md → Yapay zekâ modeli ve kullanımı.
/// </summary>
[Collection("Database")]
public class AiUsageTests(DatabaseFixture database)
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
                Code = $"kul-{Guid.NewGuid():N}"[..20], Name = "Kullanım A.Ş.", CreatedAt = DateTime.UtcNow,
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

        // An analyst who is also the company administrator: opens requests and reads the usage.
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

    private async Task<T> UsageAsync<T>(Company company, Func<AiUsageService, Task<T>> action, bool isAdmin = true)
    {
        await using var scope = database.CreateScope(company.TenantId);
        var service = new AiUsageService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
            new TestUser(company.AdminId, company.TenantId, isTenantAdmin: isAdmin),
            new FakeAgentClient(),
            TimeProvider.System);
        return await action(service);
    }

    private async Task<List<AiUsage>> RowsAsync(Company company)
    {
        await using var scope = database.CreateScope(company.TenantId);
        return await scope.ServiceProvider.GetRequiredService<IApplicationDbContext>().AiUsages
            .AsNoTracking().OrderBy(u => u.Id).ToListAsync();
    }

    private static SuggestMeetingItemsDto Notes() =>
        new() { MeetingDate = new DateOnly(2026, 11, 26), Notes = "Ekran taslaklarını Can hazırlayacak." };

    private static FakeAgentClient Answering(int input, int output, string model = "gemini-test-flash") => new()
    {
        Result = new("Özet", [], 0, model, Usage: new AgentUsage(model, input, output, null)),
    };

    // ---- The model and the record ----

    [Fact]
    public async Task Suggest_SendsTheCompanysModel_AndWritesWhatItUsed()
    {
        var company = await CreateCompanyAsync(model: "gemini-test-pro");
        var projectId = await TestProjects.CreateAsync(database, "6", tenantId: company.TenantId);
        var agent = Answering(800, 200, model: "gemini-test-flash");

        await AgentsAsync(company, agent, s => s.SuggestMeetingItemsAsync(projectId, Notes(), CancellationToken.None));

        Assert.Equal("gemini-test-pro", agent.LastRequest!.Model);
        var row = Assert.Single(await RowsAsync(company));
        // The answering model is written: here the service fell back from the chosen one.
        Assert.Equal(
            (company.AdminId, (int?)projectId, AiAssistant.MeetingSuggestions, "gemini-test-flash", (int?)800, (int?)200, true),
            (row.UserId, row.ProjectId, row.Assistant, row.Model, row.InputTokens, row.OutputTokens, row.Succeeded));
    }

    [Fact]
    public async Task Suggest_NoCompanyModel_SendsNothingSoTheServiceDefaultIsUsed()
    {
        var company = await CreateCompanyAsync();
        var agent = new FakeAgentClient();

        await AgentsAsync(company, agent, s =>
            s.DraftRequestFromEmailAsync(new DraftRequestFromEmailDto { EmailText = "Yeni ekran istiyoruz." }, CancellationToken.None));

        Assert.Null(agent.LastRequestDraftRequest!.Model);
        var row = Assert.Single(await RowsAsync(company));
        Assert.Equal((AiAssistant.RequestDraft, (int?)null), (row.Assistant, row.ProjectId));
    }

    [Fact]
    public async Task Suggest_ServiceFails_TheFailedCallIsWrittenToo()
    {
        var company = await CreateCompanyAsync(model: "gemini-test-pro");
        var projectId = await TestProjects.CreateAsync(database, "6", tenantId: company.TenantId);
        var agent = new FakeAgentClient { Failure = new ServiceUnavailableException("Yoğun.") };

        await Assert.ThrowsAsync<ServiceUnavailableException>(() =>
            AgentsAsync(company, agent, s => s.SuggestMeetingItemsAsync(projectId, Notes(), CancellationToken.None)));

        var row = Assert.Single(await RowsAsync(company));
        Assert.Equal((false, "gemini-test-pro", (int?)null), (row.Succeeded, row.Model, row.InputTokens));
    }

    [Fact]
    public async Task Transcribe_WritesTheSecondsAndNoTokens()
    {
        var company = await CreateCompanyAsync();
        var projectId = await TestProjects.CreateAsync(database, "6", tenantId: company.TenantId);
        var bytes = Encoding.UTF8.GetBytes("ses");

        await AgentsAsync(company, new FakeAgentClient(), s =>
            s.TranscribeMeetingAsync(projectId, new MemoryStream(bytes), bytes.Length, CancellationToken.None));

        var row = Assert.Single(await RowsAsync(company));
        Assert.Equal((AiAssistant.Transcription, "test-whisper", (int?)61, (int?)null),
            (row.Assistant, row.Model, row.AudioSeconds, row.InputTokens));
    }

    // ---- The monthly limit ----

    [Fact]
    public async Task Suggest_LimitReached_RefusesWithoutCallingTheService()
    {
        var company = await CreateCompanyAsync(limit: 1000);
        var projectId = await TestProjects.CreateAsync(database, "6", tenantId: company.TenantId);
        await AgentsAsync(company, Answering(800, 200), s => s.SuggestMeetingItemsAsync(projectId, Notes(), CancellationToken.None));

        var agent = new FakeAgentClient();
        var error = await Assert.ThrowsAsync<ServiceUnavailableException>(() =>
            AgentsAsync(company, agent, s => s.SuggestMeetingItemsAsync(projectId, Notes(), CancellationToken.None)));

        Assert.Equal(AiUsageRules.LimitReachedMessage, error.Message);
        Assert.False(agent.WasCalled);
    }

    [Fact]
    public async Task Suggest_UnderTheLimit_IsAllowed()
    {
        var company = await CreateCompanyAsync(limit: 1001);
        var projectId = await TestProjects.CreateAsync(database, "6", tenantId: company.TenantId);
        await AgentsAsync(company, Answering(800, 200), s => s.SuggestMeetingItemsAsync(projectId, Notes(), CancellationToken.None));

        await AgentsAsync(company, Answering(1, 1), s => s.SuggestMeetingItemsAsync(projectId, Notes(), CancellationToken.None));

        Assert.Equal(2, (await RowsAsync(company)).Count);
    }

    // ---- The month's summary ----

    [Fact]
    public async Task GetMonth_AddsUpByAssistant_AndShowsTheLimit()
    {
        var company = await CreateCompanyAsync(limit: 50_000);
        var projectId = await TestProjects.CreateAsync(database, "6", tenantId: company.TenantId);
        await AgentsAsync(company, Answering(800, 200), s => s.SuggestMeetingItemsAsync(projectId, Notes(), CancellationToken.None));
        await AgentsAsync(company, Answering(100, 50), s => s.SuggestMeetingItemsAsync(projectId, Notes(), CancellationToken.None));
        await Assert.ThrowsAsync<ServiceUnavailableException>(() => AgentsAsync(
            company, new FakeAgentClient { Failure = new ServiceUnavailableException("Yoğun.") },
            s => s.DraftRequestFromEmailAsync(new DraftRequestFromEmailDto { EmailText = "Talep." }, CancellationToken.None)));

        var summary = await UsageAsync(company, s => s.GetMonthAsync(null, CancellationToken.None));

        Assert.Equal(AiUsageRules.MonthOf(DateTime.UtcNow).ToString(), summary.Month);
        Assert.Equal((3, 1, 900L, 250L, 50_000), (summary.Requests, summary.FailedRequests, summary.InputTokens, summary.OutputTokens, summary.Limit));
        Assert.Equal(
            [new AiUsageByAssistantDto(AiAssistant.MeetingSuggestions, 2, 1150), new AiUsageByAssistantDto(AiAssistant.RequestDraft, 1, 0)],
            summary.ByAssistant);
    }

    [Fact]
    public async Task GetMonth_AnotherMonth_IsEmpty_AndABadMonthIs400()
    {
        var company = await CreateCompanyAsync();
        var projectId = await TestProjects.CreateAsync(database, "6", tenantId: company.TenantId);
        await AgentsAsync(company, Answering(10, 10), s => s.SuggestMeetingItemsAsync(projectId, Notes(), CancellationToken.None));

        var past = await UsageAsync(company, s => s.GetMonthAsync("2020-01", CancellationToken.None));

        Assert.Equal(("2020-01", 0), (past.Month, past.Requests));
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            UsageAsync(company, s => s.GetMonthAsync("11/2026", CancellationToken.None)));
        Assert.Equal(AiUsageRules.MonthFormatMessage, error.Message);
    }

    [Fact]
    public async Task GetMonthAndModels_NotAdmin_Forbidden()
    {
        var company = await CreateCompanyAsync();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            UsageAsync(company, s => s.GetMonthAsync(null, CancellationToken.None), isAdmin: false));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            UsageAsync(company, s => s.ListModelsAsync(CancellationToken.None), isAdmin: false));
    }

    [Fact]
    public async Task GetMonth_AnotherCompanysUsage_IsNotCounted()
    {
        var mine = await CreateCompanyAsync();
        var other = await CreateCompanyAsync();
        var projectId = await TestProjects.CreateAsync(database, "6", tenantId: other.TenantId);
        await AgentsAsync(other, Answering(10, 10), s => s.SuggestMeetingItemsAsync(projectId, Notes(), CancellationToken.None));

        var summary = await UsageAsync(mine, s => s.GetMonthAsync(null, CancellationToken.None));

        Assert.Equal(0, summary.Requests);
    }

    // ---- The model choice in the settings ----

    private async Task<TenantSettingsDto> UpdateSettingsAsync(Company company, string? model, int? limit)
    {
        await using var scope = database.CreateScope(company.TenantId);
        var service = new TenantSettingsService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
            new TestUser(company.AdminId, company.TenantId, isTenantAdmin: true),
            new FakeAgentClient());
        return await service.UpdateAsync(
            new UpdateTenantSettingsDto
            {
                AiEnabled = true, AiDataSharingAllowed = true, AiModel = model, AiMonthlyTokenLimit = limit,
            },
            CancellationToken.None);
    }

    [Fact]
    public async Task UpdateSettings_ModelTheServiceOffers_IsSavedWithTheLimit()
    {
        var company = await CreateCompanyAsync();

        var saved = await UpdateSettingsAsync(company, "gemini-test-pro", 200_000);

        Assert.Equal(new TenantSettingsDto(true, true, "gemini-test-pro", 200_000), saved);
    }

    [Fact]
    public async Task UpdateSettings_UnknownModel_IsAFieldError()
    {
        var company = await CreateCompanyAsync();

        var error = await Assert.ThrowsAsync<FieldValidationException>(() =>
            UpdateSettingsAsync(company, "gemini-yok", null));

        Assert.Equal(TenantSettingsService.ModelNotAvailable, Assert.Single(error.Errors["aiModel"]));
    }

    [Fact]
    public async Task UpdateSettings_TheSavedModel_StaysEvenWhenTheServiceNoLongerOffersIt()
    {
        var company = await CreateCompanyAsync(model: "gemini-eski");

        var saved = await UpdateSettingsAsync(company, "gemini-eski", null);

        Assert.Equal("gemini-eski", saved.AiModel);
    }

    [Fact]
    public async Task UpdateSettings_EmptyModel_GoesBackToTheServiceDefault()
    {
        var company = await CreateCompanyAsync(model: "gemini-test-pro", limit: 10);

        var saved = await UpdateSettingsAsync(company, "  ", null);

        Assert.Equal((null, null), (saved.AiModel, saved.AiMonthlyTokenLimit));
    }
}

/// <summary>The UTC month rules (Faz 7 · A9), without a database.</summary>
public class AiUsageRulesTests
{
    [Fact]
    public void MonthOf_IsTheUtcCalendarMonth()
    {
        var month = AiUsageRules.MonthOf(new DateTime(2026, 12, 31, 23, 59, 0, DateTimeKind.Utc));

        Assert.Equal(new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc), month.Start);
        Assert.Equal(new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc), month.End);
        Assert.Equal("2026-12", month.ToString());
    }

    [Theory]
    [InlineData("2026-11", "2026-11")]
    [InlineData("2026-13", null)]
    [InlineData("11-2026", null)]
    [InlineData("", null)]
    public void ParseMonth_OnlyYearDashMonth(string text, string? expected)
    {
        Assert.Equal(expected, AiUsageRules.ParseMonth(text)?.ToString());
    }
}
