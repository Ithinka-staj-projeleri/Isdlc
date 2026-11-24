using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Common.Models;
using Isdlc.Application.Risks;
using Isdlc.Application.Risks.Dtos;
using Isdlc.Application.Tests.Common;
using Isdlc.Domain.Risks;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Tenants;

/// <summary>
/// Risks across companies (Faz 6 · T7): Ithinka's risk cannot be listed, read or changed by Deneme, and Deneme cannot
/// make an Ithinka user the owner. Expectations: docs/multi-tenancy.md → Testler.
/// Cross test (PLANNING.md): Erva → Baran's modules.
/// </summary>
[Collection("Database")]
public class RiskIsolationTests(DatabaseFixture database)
{
    private async Task<T> AsAsync<T>(int tenantId, int userId, Func<RiskService, Task<T>> action)
    {
        await using var scope = database.CreateScope(tenantId);
        var service = new RiskService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
            new TestUser(userId, tenantId),
            new RecordingNotificationService());
        return await action(service);
    }

    private static CreateRiskDto NewRisk(int ownerUserId) => new()
    {
        Title = "Test ortamı gecikebilir", Category = RiskCategory.Technical, Probability = 3, Impact = 3,
        OwnerUserId = ownerUserId,
    };

    /// <summary>An Ithinka project with one risk, and a Deneme analyst who will try to reach it.</summary>
    private async Task<(int ProjectId, int RiskId, int IthinkaAnalystId, int DenemeAnalystId)> ArrangeAsync()
    {
        var ithinkaAnalystId = await TestProjects.UserIdAsync(database, TestProjects.Analyst);
        var denemeAnalystId = await TestProjects.UserIdAsync(database, TestTenants.DenemeAnalyst, database.DenemeId);
        var projectId = await TestProjects.CreateAsync(database, "8");
        var risk = await AsAsync(Domain.Tenants.Tenant.IthinkaId, ithinkaAnalystId,
            s => s.CreateAsync(projectId, NewRisk(ithinkaAnalystId), CancellationToken.None));
        return (projectId, risk.Id, ithinkaAnalystId, denemeAnalystId);
    }

    [Fact]
    public async Task OtherCompany_CannotListReadOrScoreTheProjectsRisks()
    {
        var (projectId, riskId, _, denemeAnalystId) = await ArrangeAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => AsAsync(database.DenemeId, denemeAnalystId,
            s => s.ListAsync(projectId, new RiskFilter(), new PageRequest(), CancellationToken.None)));
        await Assert.ThrowsAsync<NotFoundException>(() => AsAsync(database.DenemeId, denemeAnalystId,
            s => s.GetAsync(projectId, riskId, CancellationToken.None)));
        await Assert.ThrowsAsync<NotFoundException>(() => AsAsync(database.DenemeId, denemeAnalystId,
            s => s.GetMatrixAsync(projectId, CancellationToken.None)));
    }

    [Fact]
    public async Task OtherCompany_CannotChangeTheRisk()
    {
        var (projectId, riskId, ithinkaAnalystId, denemeAnalystId) = await ArrangeAsync();
        var update = new UpdateRiskDto
        {
            Title = "Başka şirket değiştirdi", Category = RiskCategory.Technical, Probability = 1, Impact = 1,
            OwnerUserId = denemeAnalystId, Status = RiskStatus.Closed,
        };

        await Assert.ThrowsAsync<NotFoundException>(() => AsAsync(database.DenemeId, denemeAnalystId,
            s => s.UpdateAsync(projectId, riskId, update, CancellationToken.None)));

        var risk = await AsAsync(Domain.Tenants.Tenant.IthinkaId, ithinkaAnalystId,
            s => s.GetAsync(projectId, riskId, CancellationToken.None));
        Assert.Equal("Test ortamı gecikebilir", risk.Title);
    }

    [Fact]
    public async Task OtherCompany_CannotReachTheRiskThroughItsOwnProject()
    {
        // Deneme's own project id with Ithinka's risk id: the risk is not Deneme's, whatever the project.
        var (_, riskId, _, denemeAnalystId) = await ArrangeAsync();
        var denemeProjectId = await TestProjects.CreateAsync(database, "8", tenantId: database.DenemeId);

        await Assert.ThrowsAsync<NotFoundException>(() => AsAsync(database.DenemeId, denemeAnalystId,
            s => s.GetAsync(denemeProjectId, riskId, CancellationToken.None)));
    }

    [Fact]
    public async Task OtherCompany_CannotMakeAnotherCompanysUserTheOwner()
    {
        var (_, _, ithinkaAnalystId, denemeAnalystId) = await ArrangeAsync();
        var denemeProjectId = await TestProjects.CreateAsync(database, "8", tenantId: database.DenemeId);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => AsAsync(database.DenemeId, denemeAnalystId,
            s => s.CreateAsync(denemeProjectId, NewRisk(ithinkaAnalystId), CancellationToken.None)));

        Assert.Equal("Seçilen sorumlu aktif bir kullanıcı değil.", error.Message);
    }

    [Fact]
    public async Task EachCompany_NumbersItsProjectsRisksOnItsOwn()
    {
        var denemeAnalystId = await TestProjects.UserIdAsync(database, TestTenants.DenemeAnalyst, database.DenemeId);
        var denemeProjectId = await TestProjects.CreateAsync(database, "8", tenantId: database.DenemeId);

        var risk = await AsAsync(database.DenemeId, denemeAnalystId,
            s => s.CreateAsync(denemeProjectId, NewRisk(denemeAnalystId), CancellationToken.None));

        Assert.Equal("RSK-001", risk.Code);
    }
}
