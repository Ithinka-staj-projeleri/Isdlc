using Isdlc.Application.Agents;
using Isdlc.Application.Agents.Dtos;
using Isdlc.Application.Audit;
using Isdlc.Application.Audit.Dtos;
using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Common.Models;
using Isdlc.Application.Tests.Agents;
using Isdlc.Application.Tests.Common;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Tenants;

/// <summary>
/// Kayıt Geçmişi, the timeline and the AI assistants across companies (Faz 6 · T7): another company's project has no
/// history or timeline for Deneme, and the assistants refuse it before calling the AI service. A company's own history
/// shows only its own people. Expectations: docs/multi-tenancy.md → Testler.
/// Cross test (PLANNING.md): Dide → Erva's modules.
/// </summary>
[Collection("Database")]
public class AuditAgentIsolationTests(DatabaseFixture database)
{
    private static readonly PageRequest FirstPage = new() { Page = 1, PageSize = 100 };

    private async Task<T> InScopeAsync<T>(int tenantId, Func<IApplicationDbContext, Task<T>> action)
    {
        await using var scope = database.CreateScope(tenantId);
        return await action(scope.ServiceProvider.GetRequiredService<IApplicationDbContext>());
    }

    private async Task<T> AgentsAsync<T>(FakeAgentClient agent, Func<AgentService, Task<T>> action)
    {
        var denemeAnalystId = await TestProjects.UserIdAsync(database, TestTenants.DenemeAnalyst, database.DenemeId);
        await using var scope = database.CreateScope(database.DenemeId);
        var service = new AgentService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
            new TestUser(denemeAnalystId, database.DenemeId),
            agent);
        return await action(service);
    }

    // ---- Kayıt Geçmişi and the timeline ----

    [Fact]
    public async Task History_OtherCompanysProject_IsNotFound()
    {
        var projectId = await TestProjects.CreateAsync(database, "4");

        await Assert.ThrowsAsync<NotFoundException>(() => InScopeAsync(database.DenemeId,
            db => new AuditService(db).ListProjectLogAsync(projectId, new AuditLogFilter(), FirstPage, CancellationToken.None)));
        await Assert.ThrowsAsync<NotFoundException>(() => InScopeAsync(database.DenemeId,
            db => new TimelineService(db).ListProjectTimelineAsync(projectId, FirstPage, CancellationToken.None)));
    }

    [Fact]
    public async Task History_OwnProject_ShowsOnlyTheCompanysOwnPeople()
    {
        var denemeUserIds = new List<int>();
        foreach (var email in new[] { TestTenants.DenemeAnalyst, TestTenants.DenemeManager, TestTenants.DenemeProjectManager, TestTenants.DenemeDeveloper })
        {
            denemeUserIds.Add(await TestProjects.UserIdAsync(database, email, database.DenemeId));
        }
        var projectId = await TestProjects.CreateAsync(database, "4", tenantId: database.DenemeId);

        var log = await InScopeAsync(database.DenemeId,
            db => new AuditService(db).ListProjectLogAsync(projectId, new AuditLogFilter(), FirstPage, CancellationToken.None));
        var timeline = await InScopeAsync(database.DenemeId,
            db => new TimelineService(db).ListProjectTimelineAsync(projectId, FirstPage, CancellationToken.None));

        Assert.NotEmpty(log.Items);
        Assert.All(log.Items, l => Assert.Contains(l.PerformedBy.Id, denemeUserIds));
        Assert.All(timeline.Items, e => Assert.Contains(e.PerformedBy.Id, denemeUserIds));
    }

    // ---- AI assistants: the project check comes before the service ----

    [Fact]
    public async Task Assistants_OtherCompanysProject_IsNotFoundAndTheServiceIsNotCalled()
    {
        var projectId = await TestProjects.CreateAsync(database, "8");
        var agent = new FakeAgentClient();

        await Assert.ThrowsAsync<NotFoundException>(() => AgentsAsync(agent, s => s.SuggestMeetingItemsAsync(projectId,
            new SuggestMeetingItemsDto { MeetingDate = new DateOnly(2026, 11, 20), Notes = "Can raporu hazırlayacak." },
            CancellationToken.None)));
        await Assert.ThrowsAsync<NotFoundException>(() => AgentsAsync(agent, s => s.SuggestRisksAsync(projectId,
            new SuggestRisksDto { SourceText = "Test ortamı gecikebilir." }, CancellationToken.None)));
        await Assert.ThrowsAsync<NotFoundException>(async () =>
        {
            await using var recording = new MemoryStream("ses"u8.ToArray());
            await AgentsAsync(agent, s => s.TranscribeMeetingAsync(projectId, recording, recording.Length, CancellationToken.None));
        });

        Assert.False(agent.WasCalled);
    }
}
