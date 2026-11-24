using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Projects;
using Isdlc.Application.Tests.Common;
using Isdlc.Application.Tests.Identity;
using Isdlc.Domain.Audit;
using Isdlc.Domain.Process;
using Isdlc.Domain.Tenants;
using Isdlc.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Tenants;

/// <summary>
/// The separation of companies on a real database (Faz 6 · T2): the query filter, TenantInterceptor, uniqueness and
/// counters per company, the audit log. Each test opens its own second company next to Ithinka (the seed).
/// Expectations: docs/multi-tenancy.md → Okuma kuralı, Yazma kuralı, Benzersizlik.
/// </summary>
[Collection("Database")]
public class TenantIsolationTests(DatabaseFixture database)
{
    private async Task<int> CreateTenantAsync(bool isActive = true)
    {
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();
        var code = $"deneme-{Guid.NewGuid():N}"[..20];
        var tenant = new Tenant { Code = code, Name = "Deneme A.Ş.", IsActive = isActive, CreatedAt = DateTime.UtcNow };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        return tenant.Id;
    }

    private static ProcessFlow Flow(string code = ProcessFlowCodes.NewRequest) => new() { Code = code, Name = "Deneme akışı" };

    private async Task<int> AddFlowAsync(int tenantId, string code = ProcessFlowCodes.NewRequest)
    {
        await using var scope = database.CreateScope(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();
        var flow = Flow(code);
        db.ProcessFlows.Add(flow);
        await db.SaveChangesAsync();
        return flow.Id;
    }

    // ---- Reading ----

    [Fact]
    public async Task Read_NewCompany_SeesNoneOfIthinkasRecords()
    {
        var tenantId = await CreateTenantAsync();

        await using var scope = database.CreateScope(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        Assert.Equal(0, await db.Projects.CountAsync());
        Assert.Equal(0, await db.Users.CountAsync());
        Assert.Equal(0, await db.ProcessFlows.CountAsync());
        Assert.Equal(0, await db.QuestionSets.CountAsync());
        Assert.Equal(0, await db.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task Read_Ithinka_StillSeesItsSeed()
    {
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        Assert.True(await db.Projects.AnyAsync());
        Assert.True(await db.ProcessFlows.AnyAsync(f => f.Code == ProcessFlowCodes.NewRequest));
        Assert.True(await db.Users.AnyAsync(u => u.Email == TestProjects.Analyst));
    }

    [Fact]
    public async Task Read_NoCompany_SeesNothing()
    {
        await using var scope = database.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(null);
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        Assert.Equal(0, await db.Projects.CountAsync());
        Assert.Equal(0, await db.Users.CountAsync());
    }

    [Fact]
    public async Task Read_UserOfAnotherCompany_IsNotFound()
    {
        var tenantId = await CreateTenantAsync();
        var user = await TestUsers.CreateAsync(database, password: null, tenantId: tenantId);

        await using var ithinka = database.CreateScope();
        await using var own = database.CreateScope(tenantId);

        Assert.Equal(tenantId, user.TenantId);
        Assert.False(await ithinka.ServiceProvider.GetRequiredService<IApplicationDbContext>().Users.AnyAsync(u => u.Id == user.Id));
        Assert.True(await own.ServiceProvider.GetRequiredService<IApplicationDbContext>().Users.AnyAsync(u => u.Id == user.Id));
    }

    // ---- Writing ----

    [Fact]
    public async Task Save_NewRecord_GetsTheScopesCompanyAndIsHiddenFromIthinka()
    {
        var tenantId = await CreateTenantAsync();

        var flowId = await AddFlowAsync(tenantId);

        await using var own = database.CreateScope(tenantId);
        await using var ithinka = database.CreateScope();
        var saved = await own.ServiceProvider.GetRequiredService<IApplicationDbContext>().ProcessFlows.SingleAsync(f => f.Id == flowId);
        Assert.Equal(tenantId, saved.TenantId);
        Assert.False(await ithinka.ServiceProvider.GetRequiredService<IApplicationDbContext>().ProcessFlows.AnyAsync(f => f.Id == flowId));
    }

    [Fact]
    public async Task Save_SameCodeAsIthinka_IsAllowedInAnotherCompany()
    {
        // Ithinka already has "new-software-project"; the code is unique per company (decision 9).
        var tenantId = await CreateTenantAsync();

        var flowId = await AddFlowAsync(tenantId, ProcessFlowCodes.NewRequest);

        Assert.True(flowId > 0);
    }

    [Fact]
    public async Task Save_SameCodeTwiceInOneCompany_IsRefused()
    {
        var tenantId = await CreateTenantAsync();
        await AddFlowAsync(tenantId);

        await Assert.ThrowsAsync<DbUpdateException>(() => AddFlowAsync(tenantId));
    }

    [Fact]
    public async Task Save_RecordOfAnotherCompany_IsRefusedAndNothingIsSaved()
    {
        var tenantId = await CreateTenantAsync();
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();
        var flow = Flow($"x-{Guid.NewGuid():N}"[..20]);
        flow.TenantId = tenantId;
        db.ProcessFlows.Add(flow);

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());

        await using var own = database.CreateScope(tenantId);
        Assert.Equal(0, await own.ServiceProvider.GetRequiredService<IApplicationDbContext>().ProcessFlows.CountAsync());
    }

    [Fact]
    public async Task Save_MovingARecordToAnotherCompany_IsRefused()
    {
        var tenantId = await CreateTenantAsync();
        var flowId = await AddFlowAsync(tenantId);
        await using var scope = database.CreateScope(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();
        var flow = await db.ProcessFlows.SingleAsync(f => f.Id == flowId);

        flow.TenantId = Tenant.IthinkaId;

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Save_NoCompany_IsRefused()
    {
        await using var scope = database.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(null);
        var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();
        db.ProcessFlows.Add(Flow($"x-{Guid.NewGuid():N}"[..20]));

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    // ---- Counters and the audit log ----

    [Fact]
    public async Task Counter_ProjectNumbers_StartAgainInEachCompany()
    {
        var tenantId = await CreateTenantAsync();
        await using var own = database.CreateScope(tenantId);
        await using var ithinka = database.CreateScope();

        var next = ProjectCodes.NextNumber(await HighestProjectNumberAsync(own));
        var ithinkaNext = ProjectCodes.NextNumber(await HighestProjectNumberAsync(ithinka));

        Assert.Equal(1, next);
        Assert.True(ithinkaNext > 15, $"Ithinka's seed has projects 14 and 15; got {ithinkaNext}.");
    }

    /// <summary>The query ProjectService numbers a new request with (plain numbers only), without its flow condition.</summary>
    private static Task<string?> HighestProjectNumberAsync(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<IApplicationDbContext>().Projects
            .Where(p => !EF.Functions.Like(p.Code, "%[^0-9]%"))
            .OrderByDescending(p => p.Code.Length)
            .ThenByDescending(p => p.Code)
            .Select(p => p.Code)
            .FirstOrDefaultAsync();

    [Fact]
    public async Task AuditLog_BelongsToTheRecordsCompanyAndDoesNotListTheCompanyAsAChange()
    {
        var projectId = await TestProjects.CreateAsync(database, "1");

        await using var scope = database.CreateScope();
        var log = await scope.ServiceProvider.GetRequiredService<IApplicationDbContext>().AuditLogs
            .SingleAsync(a => a.EntityName == "Project" && a.EntityId == projectId && a.Action == AuditAction.Created);

        Assert.Equal(Tenant.IthinkaId, log.TenantId);
        Assert.DoesNotContain("TenantId", log.Changes);
    }
}
