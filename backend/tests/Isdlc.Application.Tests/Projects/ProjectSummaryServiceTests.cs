using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Projects;
using Isdlc.Application.Projects.Dtos;
using Isdlc.Application.Tests.Common;
using Isdlc.Domain.Documents;
using Isdlc.Domain.OpenIssues;
using Isdlc.Domain.Requirements;
using Isdlc.Domain.Risks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Projects;

/// <summary>
/// The project overview (Faz 5 · F1) on a real database: GET /projects/{id}/summary reads the project's own records.
/// Every test creates its own project (TestProjects), so seed projects are never changed. The counting itself is tested
/// in ProjectSummaryRulesTests. Cross test (PLANNING.md): Baran → Dide's modules.
/// </summary>
[Collection("Database")]
public class ProjectSummaryServiceTests(DatabaseFixture database)
{
    private static readonly DateOnly Today = new(2026, 11, 23);

    private sealed class FixedToday : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);
    }

    private async Task<ProjectSummaryDto> SummaryAsync(int projectId)
    {
        await using var scope = database.CreateScope();
        var service = new ProjectSummaryService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(), new FixedToday());
        return await service.GetAsync(projectId, CancellationToken.None);
    }

    /// <summary>Writes the project's records straight to the database.</summary>
    private async Task AddAsync(int projectId, Action<IApplicationDbContext, int> add)
    {
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        add(db, projectId);
        await db.SaveChangesAsync();
    }

    private static OpenIssue Issue(int projectId, string code, OpenIssueStatus status, int? ownerUserId = null,
        string? ownerName = null, DateOnly? due = null, DateTime? closedAt = null) => new()
    {
        ProjectId = projectId,
        Code = code,
        Type = OpenIssueType.Action,
        Title = $"Aksiyon {code}",
        OwnerUserId = ownerUserId,
        OwnerName = ownerUserId is null ? ownerName ?? "Müşteri BT" : null,
        DueDate = due,
        Status = status,
        ClosedAt = closedAt,
    };

    [Fact]
    public async Task Get_CountsTheProjectsOwnRecords()
    {
        var projectId = await TestProjects.CreateAsync(database, "12");
        var otherId = await TestProjects.CreateAsync(database, "12");
        var analystId = await TestProjects.UserIdAsync(database, TestProjects.Analyst);
        var documentTypeIds = await DocumentTypeIdsAsync();

        await AddAsync(projectId, (db, id) =>
        {
            db.OpenIssues.AddRange(
                Issue(id, "AK-001", OpenIssueStatus.Open, ownerUserId: analystId, due: Today.AddDays(-3)),
                Issue(id, "AK-002", OpenIssueStatus.InProgress, ownerUserId: analystId),
                Issue(id, "AK-003", OpenIssueStatus.Open, ownerName: "Müşteri BT"),
                Issue(id, "AK-004", OpenIssueStatus.Closed, ownerName: "Müşteri BT", closedAt: new DateTime(2026, 11, 20)));
            db.Risks.AddRange(
                new Risk { ProjectId = id, Code = "RSK-001", Title = "Entegrasyon", Category = RiskCategory.Technical, Probability = 4, Impact = 5, OwnerUserId = analystId },
                new Risk { ProjectId = id, Code = "RSK-002", Title = "Kapsam", Category = RiskCategory.Scope, Probability = 1, Impact = 2, Status = RiskStatus.Closed, OwnerUserId = analystId });
            db.Requirements.AddRange(
                new Requirement { ProjectId = id, Code = "GR-001", Type = RequirementType.Functional, Title = "Giriş", Priority = RequirementPriority.MustHave, Status = RequirementStatus.Approved },
                new Requirement { ProjectId = id, Code = "GR-002", Type = RequirementType.Functional, Title = "Rapor", Priority = RequirementPriority.ShouldHave });
            db.ProjectDocuments.AddRange(
                new ProjectDocument { ProjectId = id, DocumentTypeId = documentTypeIds[0], Status = DocumentStatus.Approved },
                new ProjectDocument { ProjectId = id, DocumentTypeId = documentTypeIds[1], Status = DocumentStatus.NotRequired });
        });
        // Another project's record is never counted.
        await AddAsync(otherId, (db, id) => db.OpenIssues.Add(Issue(id, "AK-001", OpenIssueStatus.Open)));

        var summary = await SummaryAsync(projectId);

        Assert.Equal((2, 1, 1, 1, 1), (summary.OpenIssues.Open, summary.OpenIssues.InProgress, summary.OpenIssues.Closed,
            summary.OpenIssues.Overdue, summary.OpenIssues.ClosedLast30Days));
        Assert.Equal(3, summary.OpenIssues.ByType.Single(t => t.Type == OpenIssueType.Action).Count);
        Assert.Equal(new RiskSummaryDto(High: 1, Medium: 0, Low: 0, Closed: 1), summary.Risks);
        Assert.Equal(new RequirementSummaryDto(Draft: 1, InReview: 0, Approved: 1, Cancelled: 0), summary.Requirements);
        Assert.Equal(new DocumentSummaryDto(Approved: 1, Tracked: documentTypeIds.Count - 1), summary.Documents);
        Assert.Equal(
            [new OwnerWorkloadDto(analystId, "Elif Yılmaz", Open: 2, Overdue: 1), new OwnerWorkloadDto(null, "Müşteri BT", Open: 1, Overdue: 0)],
            summary.Workload);
    }

    [Fact]
    public async Task Get_ProjectWithoutRecords_IsEmptyButTracksEveryDocumentType()
    {
        var projectId = await TestProjects.CreateAsync(database, "1");

        var summary = await SummaryAsync(projectId);

        Assert.Equal(0, summary.OpenIssues.Open + summary.OpenIssues.Closed);
        Assert.Equal(new RiskSummaryDto(0, 0, 0, 0), summary.Risks);
        Assert.Equal(new DocumentSummaryDto(Approved: 0, Tracked: (await DocumentTypeIdsAsync()).Count), summary.Documents);
        Assert.Empty(summary.Workload);
    }

    [Fact]
    public async Task Get_UnknownProject_Throws404()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => SummaryAsync(int.MaxValue));
    }

    private async Task<List<int>> DocumentTypeIdsAsync()
    {
        await using var scope = database.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IApplicationDbContext>().DocumentTypes
            .OrderBy(t => t.Id)
            .Select(t => t.Id)
            .ToListAsync();
    }
}
