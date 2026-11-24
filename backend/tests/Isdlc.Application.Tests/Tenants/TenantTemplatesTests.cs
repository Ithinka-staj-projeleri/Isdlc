using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Tests.Common;
using Isdlc.Domain.Process;
using Isdlc.Domain.Tenants;
using Isdlc.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Tenants;

/// <summary>
/// The template seed per company (Faz 6 · T3): the startup gives every active company the templates, a new company
/// gets the same copy, and copies the company changed are left alone.
/// Expectations: docs/multi-tenancy.md → Seed ve şablon. Tests that change templates open their own company.
/// </summary>
[Collection("Database")]
public class TenantTemplatesTests(DatabaseFixture database)
{
    private record Counts(int Flows, int Stages, int Steps, int Jumps, int DocumentTypes, int QuestionSets, int Questions, int Options);

    private async Task<Counts> CountAsync(int tenantId)
    {
        await using var scope = database.CreateScope(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        return new Counts(
            await db.ProcessFlows.CountAsync(),
            await db.ProcessStages.CountAsync(),
            await db.ProcessSteps.CountAsync(),
            await db.ProcessStepJumps.CountAsync(),
            await db.DocumentTypes.CountAsync(),
            await db.QuestionSets.CountAsync(),
            await db.Questions.CountAsync(),
            await db.QuestionOptions.CountAsync());
    }

    /// <summary>A new company with nothing in it, like POST /tenants before the copy.</summary>
    private async Task<int> CreateTenantAsync()
    {
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();
        var tenant = new Tenant { Code = $"sablon-{Guid.NewGuid():N}"[..20], Name = "Şablon A.Ş.", CreatedAt = DateTime.UtcNow };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        return tenant.Id;
    }

    /// <summary>What opening a company and every startup do: set the company, apply the templates in a transaction.</summary>
    private async Task ApplyAsync(int tenantId)
    {
        await using var scope = database.CreateScope(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        await scope.ServiceProvider.GetRequiredService<ITenantTemplates>().ApplyAsync();
        await transaction.CommitAsync();
    }

    private async Task<T> InTenantAsync<T>(int tenantId, Func<IsdlcDbContext, Task<T>> action)
    {
        await using var scope = database.CreateScope(tenantId);
        return await action(scope.ServiceProvider.GetRequiredService<IsdlcDbContext>());
    }

    // ---- Startup (the fixture ran it with Deneme and an inactive company in place) ----

    [Fact]
    public async Task Startup_ActiveCompany_GetsTheSameTemplatesAsIthinka()
    {
        var ithinka = await CountAsync(Tenant.IthinkaId);
        var deneme = await CountAsync(database.DenemeId);

        Assert.Equal(ithinka, deneme);
        Assert.True(deneme.Flows > 0 && deneme.Steps > 0 && deneme.DocumentTypes > 0 && deneme.QuestionSets > 0);
    }

    [Fact]
    public async Task Startup_CopiesAreTheCompanysOwnRows_MarkedWithTheirTemplate()
    {
        var ithinkaFlowIds = await InTenantAsync(Tenant.IthinkaId, db => db.ProcessFlows.Select(f => f.Id).ToListAsync());
        var denemeFlows = await InTenantAsync(database.DenemeId, db => db.ProcessFlows.ToListAsync());

        Assert.Empty(denemeFlows.Select(f => f.Id).Intersect(ithinkaFlowIds));
        Assert.All(denemeFlows, f =>
        {
            Assert.Equal(f.Code, f.TemplateCode);
            Assert.False(f.IsCustomized);
        });
    }

    [Fact]
    public async Task Startup_InactiveCompany_GetsNoTemplates()
    {
        Assert.Equal(new Counts(0, 0, 0, 0, 0, 0, 0, 0), await CountAsync(database.InactiveTenantId));
    }

    [Fact]
    public async Task Startup_DevelopmentDataStaysWithIthinka()
    {
        // None of Seed/users.json and no sample project in Deneme. Other tests add their own users and "T-" projects to
        // Deneme, so this looks for the seed's records instead of counting (AGENTS.md → Bölüm 9).
        string[] seedEmails = [TestProjects.Analyst, TestProjects.Manager, TestProjects.ProjectManager, TestProjects.Developer, TestProjects.Sales];
        var denemeEmails = await InTenantAsync(database.DenemeId, db => db.Users.Select(u => u.Email).ToListAsync());
        var denemeHasSeedProjects = await InTenantAsync(database.DenemeId, db => db.Projects.AnyAsync(p => !p.Code.StartsWith("T-")));

        Assert.Empty(denemeEmails.Intersect(seedEmails));
        Assert.Subset(denemeEmails.ToHashSet(), TestTenants.DenemeUsers.Select(u => u.Email).ToHashSet());
        Assert.False(denemeHasSeedProjects);
    }

    [Fact]
    public async Task Startup_DenemeCanRunItsOwnProject()
    {
        var projectId = await TestProjects.CreateAsync(database, "5", tenantId: database.DenemeId);

        var project = await InTenantAsync(database.DenemeId, db => db.Projects.Include(p => p.CurrentStep).SingleAsync(p => p.Id == projectId));
        var denemeAnalystId = await TestProjects.UserIdAsync(database, TestTenants.DenemeAnalyst, database.DenemeId);

        Assert.Equal("5", project.CurrentStep!.Code);
        Assert.Equal(denemeAnalystId, project.BusinessAnalystId);
        Assert.False(await InTenantAsync(Tenant.IthinkaId, db => db.Projects.AnyAsync(p => p.Id == projectId)));
    }

    // ---- A new company (POST /tenants uses the same ApplyAsync) ----

    [Fact]
    public async Task Apply_NewCompany_GetsEveryTemplate()
    {
        var tenantId = await CreateTenantAsync();

        await ApplyAsync(tenantId);

        Assert.Equal(await CountAsync(Tenant.IthinkaId), await CountAsync(tenantId));
    }

    [Fact]
    public async Task Apply_Twice_WritesNothingNew()
    {
        var tenantId = await CreateTenantAsync();
        await ApplyAsync(tenantId);
        var first = await CountAsync(tenantId);

        await ApplyAsync(tenantId);

        Assert.Equal(first, await CountAsync(tenantId));
    }

    [Fact]
    public async Task Apply_NoCompany_Throws()
    {
        await using var scope = database.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => scope.ServiceProvider.GetRequiredService<ITenantTemplates>().ApplyAsync());
    }

    // ---- Keeping copies in line with the files ----

    [Fact]
    public async Task Apply_UnchangedCopy_FollowsTheFileAgain()
    {
        var tenantId = await CreateTenantAsync();
        await ApplyAsync(tenantId);
        var original = await SetFirstStepTextAsync(tenantId, "Elle değiştirilmiş metin", customize: false);

        await ApplyAsync(tenantId);

        Assert.Equal(original, await FirstStepTextAsync(tenantId));
    }

    [Fact]
    public async Task Apply_CustomizedFlow_IsLeftAsTheCompanyMadeIt()
    {
        var tenantId = await CreateTenantAsync();
        await ApplyAsync(tenantId);
        await SetFirstStepTextAsync(tenantId, "Şirketin kendi metni", customize: true);

        await ApplyAsync(tenantId);

        Assert.Equal("Şirketin kendi metni", await FirstStepTextAsync(tenantId));
    }

    [Fact]
    public async Task Apply_CustomizedDocumentTypeAndQuestionSet_AreLeftAlone()
    {
        var tenantId = await CreateTenantAsync();
        await ApplyAsync(tenantId);
        await InTenantAsync(tenantId, async db =>
        {
            var type = await db.DocumentTypes.OrderBy(t => t.Id).FirstAsync();
            type.Name = "Şirketin dokümanı";
            type.IsCustomized = true;
            var set = await db.QuestionSets.OrderBy(s => s.Id).FirstAsync();
            set.Name = "Şirketin soru seti";
            set.IsCustomized = true;
            return await db.SaveChangesAsync();
        });

        await ApplyAsync(tenantId);

        Assert.Equal("Şirketin dokümanı", await InTenantAsync(tenantId, db => db.DocumentTypes.OrderBy(t => t.Id).Select(t => t.Name).FirstAsync()));
        Assert.Equal("Şirketin soru seti", await InTenantAsync(tenantId, db => db.QuestionSets.OrderBy(s => s.Id).Select(s => s.Name).FirstAsync()));
    }

    [Fact]
    public async Task Apply_UnchangedDocumentType_FollowsTheFileAgain()
    {
        var tenantId = await CreateTenantAsync();
        await ApplyAsync(tenantId);
        var original = await InTenantAsync(tenantId, async db =>
        {
            var type = await db.DocumentTypes.OrderBy(t => t.Id).FirstAsync();
            var name = type.Name;
            type.Name = "Elle değiştirilmiş";
            await db.SaveChangesAsync();
            return name;
        });

        await ApplyAsync(tenantId);

        Assert.Equal(original, await InTenantAsync(tenantId, db => db.DocumentTypes.OrderBy(t => t.Id).Select(t => t.Name).FirstAsync()));
    }

    [Fact]
    public async Task Apply_CompanysOwnFlowWithATemplateCode_IsKeptAndNotDuplicated()
    {
        // The company made its own flow with the template's code before the templates came: its flow wins.
        var tenantId = await CreateTenantAsync();
        await InTenantAsync(tenantId, db =>
        {
            db.ProcessFlows.Add(new ProcessFlow { Code = ProcessFlowCodes.AddOn, Name = "Kendi akışımız" });
            return db.SaveChangesAsync();
        });

        await ApplyAsync(tenantId);

        var addOns = await InTenantAsync(tenantId, db => db.ProcessFlows.Where(f => f.Code == ProcessFlowCodes.AddOn).ToListAsync());
        var own = Assert.Single(addOns);
        Assert.Equal("Kendi akışımız", own.Name);
        Assert.Null(own.TemplateCode);
    }

    [Fact]
    public async Task Apply_OneCompany_DoesNotTouchAnother()
    {
        var tenantId = await CreateTenantAsync();
        var ithinkaBefore = await CountAsync(Tenant.IthinkaId);

        await ApplyAsync(tenantId);

        Assert.Equal(ithinkaBefore, await CountAsync(Tenant.IthinkaId));
    }

    private async Task<string> SetFirstStepTextAsync(int tenantId, string text, bool customize) =>
        await InTenantAsync(tenantId, async db =>
        {
            var flow = await db.ProcessFlows.SingleAsync(f => f.Code == ProcessFlowCodes.NewRequest);
            var step = await db.ProcessSteps.Where(s => s.ProcessFlowId == flow.Id).OrderBy(s => s.Order).FirstAsync();
            var original = step.Text;
            step.Text = text;
            flow.IsCustomized = customize;
            await db.SaveChangesAsync();
            return original;
        });

    private Task<string> FirstStepTextAsync(int tenantId) =>
        InTenantAsync(tenantId, db => db.ProcessSteps
            .Where(s => s.ProcessFlow.Code == ProcessFlowCodes.NewRequest)
            .OrderBy(s => s.Order)
            .Select(s => s.Text)
            .FirstAsync());
}
