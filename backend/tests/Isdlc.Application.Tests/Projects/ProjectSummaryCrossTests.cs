using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Projects;
using Isdlc.Application.Projects.Dtos;
using Isdlc.Application.Tests.Common;
using Isdlc.Domain.Documents;
using Isdlc.Domain.OpenIssues;
using Isdlc.Domain.Projects;
using Isdlc.Domain.Requirements;
using Isdlc.Domain.Risks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Projects;

/// <summary>
/// The project overview (Faz 5 · F1) against its contract (docs/projects-api.md → "Proje özeti", F1-1…F1-4) on a real
/// database: the boundaries of every count (due today, 30 days, risk severities), outside owners and closed projects.
/// Every test creates its own project. Cross test (PLANNING.md): Baran → Dide's modules.
/// </summary>
[Collection("Database")]
public class ProjectSummaryCrossTests(DatabaseFixture database)
{
    private static readonly DateOnly Today = new(2026, 11, 24);
    private static readonly DateTime Noon = new(2026, 11, 24, 12, 0, 0, DateTimeKind.Utc);

    private sealed class FixedNow : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(Noon);
    }

    private async Task<ProjectSummaryDto> SummaryAsync(int projectId)
    {
        await using var scope = database.CreateScope();
        var service = new ProjectSummaryService(scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(), new FixedNow());
        return await service.GetAsync(projectId, CancellationToken.None);
    }

    private async Task AddAsync(Action<IApplicationDbContext> add)
    {
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        add(db);
        await db.SaveChangesAsync();
    }

    private static OpenIssue Issue(
        int projectId, string code, OpenIssueStatus status = OpenIssueStatus.Open, int? ownerUserId = null,
        string? ownerName = "Müşteri BT", DateOnly? due = null, DateTime? closedAt = null,
        OpenIssueType type = OpenIssueType.Action) => new()
    {
        ProjectId = projectId,
        Code = code,
        Type = type,
        Title = $"Konu {code}",
        OwnerUserId = ownerUserId,
        OwnerName = ownerUserId is null ? ownerName : null,
        DueDate = due,
        Status = status,
        ClosedAt = status == OpenIssueStatus.Closed ? closedAt ?? Noon : null,
    };

    private static Risk Risk(int projectId, string code, int probability, int impact, int ownerUserId,
        RiskStatus status = RiskStatus.Open) => new()
    {
        ProjectId = projectId, Code = code, Title = $"Risk {code}", Category = RiskCategory.Technical,
        Probability = probability, Impact = impact, OwnerUserId = ownerUserId, Status = status,
    };

    // ---- Open issues (F1-1) ----

    [Fact]
    public async Task OpenIssues_DueToday_IsNotOverdue_InProgressPastDue_Is()
    {
        var projectId = await TestProjects.CreateAsync(database, "12");
        await AddAsync(db => db.OpenIssues.AddRange(
            Issue(projectId, "AK-001", due: Today),                                                   // due today: not late yet
            Issue(projectId, "AK-002", OpenIssueStatus.InProgress, due: Today.AddDays(-1)),           // late
            Issue(projectId, "AK-003", OpenIssueStatus.Closed, due: Today.AddDays(-5)),               // closed: never late
            Issue(projectId, "AK-004", due: null)));                                                  // no due date

        var issues = (await SummaryAsync(projectId)).OpenIssues;

        Assert.Equal((2, 1, 1, 1), (issues.Open, issues.InProgress, issues.Closed, issues.Overdue));
    }

    [Fact]
    public async Task OpenIssues_ClosedLast30Days_From30DaysAgoOnly()
    {
        var projectId = await TestProjects.CreateAsync(database, "12");
        await AddAsync(db => db.OpenIssues.AddRange(
            Issue(projectId, "AK-001", OpenIssueStatus.Closed, closedAt: Noon.AddDays(-30)),          // the first counted day
            Issue(projectId, "AK-002", OpenIssueStatus.Closed, closedAt: Noon.AddDays(-31)),          // a day too early
            Issue(projectId, "AK-003", OpenIssueStatus.Closed, closedAt: Noon)));

        var issues = (await SummaryAsync(projectId)).OpenIssues;

        Assert.Equal((3, 2), (issues.Closed, issues.ClosedLast30Days));
    }

    [Fact]
    public async Task OpenIssues_ByType_EveryTypeInEnumOrder_ZerosIncluded_ClosedLeftOut()
    {
        var projectId = await TestProjects.CreateAsync(database, "12");
        await AddAsync(db => db.OpenIssues.AddRange(
            Issue(projectId, "AK-001", type: OpenIssueType.Decision),
            Issue(projectId, "AK-002", type: OpenIssueType.Decision),
            Issue(projectId, "AK-003", OpenIssueStatus.InProgress, type: OpenIssueType.Question),
            Issue(projectId, "AK-004", OpenIssueStatus.Closed, type: OpenIssueType.Dependency)));

        var byType = (await SummaryAsync(projectId)).OpenIssues.ByType;

        Assert.Equal(Enum.GetValues<OpenIssueType>(), byType.Select(t => t.Type));
        Assert.Equal(2, byType.Single(t => t.Type == OpenIssueType.Decision).Count);
        Assert.Equal(1, byType.Single(t => t.Type == OpenIssueType.Question).Count);
        Assert.Equal(0, byType.Single(t => t.Type == OpenIssueType.Dependency).Count);
    }

    // ---- Risks (F1-2) ----

    [Fact]
    public async Task Risks_SeverityBoundaries_MonitoringIsFollowed_ClosedApart()
    {
        var projectId = await TestProjects.CreateAsync(database, "12");
        var ownerId = await TestProjects.UserIdAsync(database, TestProjects.Developer);
        await AddAsync(db => db.Risks.AddRange(
            Risk(projectId, "RSK-001", 2, 3, ownerId),                                  // 6: low (7 needs an impact over 5)
            Risk(projectId, "RSK-002", 2, 4, ownerId),                                  // 8: medium
            Risk(projectId, "RSK-003", 2, 5, ownerId, RiskStatus.Monitoring),           // 10: medium, followed
            Risk(projectId, "RSK-004", 3, 5, ownerId, RiskStatus.Monitoring),           // 15: high, followed
            Risk(projectId, "RSK-005", 5, 5, ownerId, RiskStatus.Closed)));              // closed: not by severity

        Assert.Equal(new RiskSummaryDto(High: 1, Medium: 2, Low: 1, Closed: 1), (await SummaryAsync(projectId)).Risks);
    }

    // ---- Requirements and documents (F1-3) ----

    [Fact]
    public async Task Requirements_EveryStatusCounted_CancelledToo()
    {
        var projectId = await TestProjects.CreateAsync(database, "12");
        RequirementStatus[] statuses =
            [RequirementStatus.Draft, RequirementStatus.InReview, RequirementStatus.InReview, RequirementStatus.Approved, RequirementStatus.Cancelled];
        await AddAsync(db => db.Requirements.AddRange(statuses.Select((status, i) => new Requirement
        {
            ProjectId = projectId, Code = $"GR-00{i + 1}", Type = RequirementType.Functional, Title = $"Gereksinim {i}",
            Priority = RequirementPriority.ShouldHave, Status = status,
        })));

        Assert.Equal(new RequirementSummaryDto(Draft: 1, InReview: 2, Approved: 1, Cancelled: 1),
            (await SummaryAsync(projectId)).Requirements);
    }

    [Fact]
    public async Task Documents_NotRequiredLeavesTracking_OnlyApprovedCountsAsApproved()
    {
        var projectId = await TestProjects.CreateAsync(database, "12");
        List<int> typeIds;
        await using (var scope = database.CreateScope())
        {
            typeIds = await scope.ServiceProvider.GetRequiredService<IApplicationDbContext>().DocumentTypes
                .OrderBy(t => t.Id).Select(t => t.Id).ToListAsync();
        }

        await AddAsync(db => db.ProjectDocuments.AddRange(
            new ProjectDocument { ProjectId = projectId, DocumentTypeId = typeIds[0], Status = DocumentStatus.Approved },
            new ProjectDocument { ProjectId = projectId, DocumentTypeId = typeIds[1], Status = DocumentStatus.InReview },
            new ProjectDocument { ProjectId = projectId, DocumentTypeId = typeIds[2], Status = DocumentStatus.NotRequired },
            new ProjectDocument { ProjectId = projectId, DocumentTypeId = typeIds[3], Status = DocumentStatus.NotRequired }));

        Assert.Equal(new DocumentSummaryDto(Approved: 1, Tracked: typeIds.Count - 2), (await SummaryAsync(projectId)).Documents);
    }

    // ---- Workload (F1-4) ----

    [Fact]
    public async Task Workload_UserAndOutsidePersonWithTheSameName_AreTwoRows()
    {
        var projectId = await TestProjects.CreateAsync(database, "12");
        var analystId = await TestProjects.UserIdAsync(database, TestProjects.Analyst);
        await AddAsync(db => db.OpenIssues.AddRange(
            Issue(projectId, "AK-001", ownerUserId: analystId),
            Issue(projectId, "AK-002", ownerName: "Elif Yılmaz"),          // someone outside with the same name
            Issue(projectId, "AK-003", ownerName: "Elif Yılmaz")));

        var workload = (await SummaryAsync(projectId)).Workload;

        Assert.Equal(
            [new OwnerWorkloadDto(null, "Elif Yılmaz", Open: 2, Overdue: 0), new OwnerWorkloadDto(analystId, "Elif Yılmaz", Open: 1, Overdue: 0)],
            workload);
    }

    [Fact]
    public async Task Workload_OrderBusiestThenMostOverdueThenNameInTurkish()
    {
        var projectId = await TestProjects.CreateAsync(database, "12");
        await AddAsync(db => db.OpenIssues.AddRange(
            Issue(projectId, "AK-001", ownerName: "Tolga"),
            Issue(projectId, "AK-002", ownerName: "Şule"),                                  // Ş comes before T in Turkish
            Issue(projectId, "AK-003", ownerName: "Ahmet", due: Today.AddDays(-1)),         // same count, more overdue
            Issue(projectId, "AK-004", ownerName: "Zeynep"),
            Issue(projectId, "AK-005", ownerName: "Zeynep"),                                // busiest
            Issue(projectId, "AK-006", OpenIssueStatus.Closed, ownerName: "Ahmet")));       // closed: not counted

        var workload = (await SummaryAsync(projectId)).Workload;

        Assert.Equal(["Zeynep", "Ahmet", "Şule", "Tolga"], workload.Select(w => w.Name));
        Assert.Equal((1, 1), (workload[1].Open, workload[1].Overdue));
    }

    // ---- Any project can be read ----

    [Theory]
    [InlineData(ProjectStatus.Closed)]
    [InlineData(ProjectStatus.Rejected)]
    [InlineData(ProjectStatus.Postponed)]
    public async Task Get_ProjectNotActive_IsStillSummarized(ProjectStatus status)
    {
        var projectId = await TestProjects.CreateAsync(database, null, status);
        await AddAsync(db => db.OpenIssues.Add(Issue(projectId, "AK-001", due: Today.AddDays(-2))));

        var issues = (await SummaryAsync(projectId)).OpenIssues;

        Assert.Equal((1, 1), (issues.Open, issues.Overdue));
    }
}
