using Isdlc.Application.Projects;
using Isdlc.Application.Projects.Dtos;
using Isdlc.Domain.Documents;
using Isdlc.Domain.OpenIssues;
using Isdlc.Domain.Requirements;
using Isdlc.Domain.Risks;

namespace Isdlc.Application.Tests.Projects;

/// <summary>
/// The project overview's counts (Faz 5 · F1), no database. Expectations: docs/projects-api.md → "Proje özeti".
/// Cross test (PLANNING.md): Baran → Dide's modules.
/// </summary>
public class ProjectSummaryRulesTests
{
    private static readonly DateOnly Today = new(2026, 11, 23);

    private static SummaryOpenIssueRow Issue(
        OpenIssueStatus status = OpenIssueStatus.Open, OpenIssueType type = OpenIssueType.Action, DateOnly? due = null,
        DateTime? closedAt = null, int? ownerUserId = null, string? ownerName = "Müşteri BT") =>
        new(type, status, due, closedAt, ownerUserId, ownerUserId is null ? ownerName : null);

    // ---- Open issues ----

    [Fact]
    public void OpenIssues_CountsByStatus()
    {
        var summary = ProjectSummaryRules.OpenIssues(
        [
            Issue(), Issue(), Issue(OpenIssueStatus.InProgress), Issue(OpenIssueStatus.Closed, closedAt: new DateTime(2026, 1, 1)),
        ], Today);

        Assert.Equal((2, 1, 1), (summary.Open, summary.InProgress, summary.Closed));
    }

    [Fact]
    public void OpenIssues_Overdue_NotClosedAndPastTheDueDate()
    {
        var summary = ProjectSummaryRules.OpenIssues(
        [
            Issue(due: Today.AddDays(-1)),                                    // overdue
            Issue(OpenIssueStatus.InProgress, due: Today.AddDays(-10)),        // overdue
            Issue(due: Today),                                                 // due today: not yet
            Issue(),                                                           // no due date
            Issue(OpenIssueStatus.Closed, due: Today.AddDays(-5), closedAt: Today.ToDateTime(TimeOnly.MinValue)),
        ], Today);

        Assert.Equal(2, summary.Overdue);
    }

    [Fact]
    public void OpenIssues_ClosedLast30Days_CountsFrom30DaysAgo()
    {
        DateTime At(int daysAgo) => Today.AddDays(-daysAgo).ToDateTime(new TimeOnly(10, 0), DateTimeKind.Utc);

        var summary = ProjectSummaryRules.OpenIssues(
        [
            Issue(OpenIssueStatus.Closed, closedAt: At(0)),
            Issue(OpenIssueStatus.Closed, closedAt: At(30)),
            Issue(OpenIssueStatus.Closed, closedAt: At(31)),
        ], Today);

        Assert.Equal(2, summary.ClosedLast30Days);
    }

    [Fact]
    public void OpenIssues_ByType_NotClosedOnly_EveryTypeInOrder()
    {
        var summary = ProjectSummaryRules.OpenIssues(
        [
            Issue(type: OpenIssueType.Question),
            Issue(OpenIssueStatus.InProgress, OpenIssueType.Question),
            Issue(type: OpenIssueType.Action),
            Issue(OpenIssueStatus.Closed, OpenIssueType.Decision, closedAt: new DateTime(2026, 1, 1)),
        ], Today);

        Assert.Equal(Enum.GetValues<OpenIssueType>(), summary.ByType.Select(t => t.Type));
        Assert.Equal([2, 1, 0, 0, 0, 0], summary.ByType.Select(t => t.Count));
    }

    [Fact]
    public void OpenIssues_None_AllZero()
    {
        var summary = ProjectSummaryRules.OpenIssues([], Today);

        Assert.Equal(0, summary.Open + summary.InProgress + summary.Closed + summary.Overdue + summary.ClosedLast30Days);
        Assert.All(summary.ByType, t => Assert.Equal(0, t.Count));
    }

    // ---- Risks ----

    [Fact]
    public void Risks_FollowedBySeverity_ClosedSeparately()
    {
        var summary = ProjectSummaryRules.Risks(
        [
            new SummaryRiskRow(5, 5, RiskStatus.Open),        // 25 high
            new SummaryRiskRow(3, 5, RiskStatus.Monitoring),  // 15 high
            new SummaryRiskRow(2, 4, RiskStatus.Open),        // 8 medium
            new SummaryRiskRow(1, 5, RiskStatus.Open),        // 5 low
            new SummaryRiskRow(5, 5, RiskStatus.Closed),      // closed: not by severity
        ]);

        Assert.Equal(new RiskSummaryDto(High: 2, Medium: 1, Low: 1, Closed: 1), summary);
    }

    // ---- Requirements and documents ----

    [Fact]
    public void Requirements_CountsByStatus()
    {
        var summary = ProjectSummaryRules.Requirements(
            [RequirementStatus.Draft, RequirementStatus.Approved, RequirementStatus.Approved, RequirementStatus.Cancelled]);

        Assert.Equal(new RequirementSummaryDto(Draft: 1, InReview: 0, Approved: 2, Cancelled: 1), summary);
    }

    [Fact]
    public void Documents_TrackedIsEveryTypeButNotRequired_TypesWithoutADocumentCount()
    {
        var summary = ProjectSummaryRules.Documents(13,
        [
            new SummaryDocumentRow(DocumentStatus.Approved),
            new SummaryDocumentRow(DocumentStatus.Approved),
            new SummaryDocumentRow(DocumentStatus.InProgress),
            new SummaryDocumentRow(DocumentStatus.NotRequired),
        ]);

        Assert.Equal(new DocumentSummaryDto(Approved: 2, Tracked: 12), summary);
    }

    // ---- Workload ----

    [Fact]
    public void Workload_NotClosedPerOwner_BusiestFirst()
    {
        var names = new Dictionary<int, string> { [1] = "Elif Yılmaz", [2] = "Ayşe Demir" };

        var workload = ProjectSummaryRules.Workload(
        [
            Issue(ownerUserId: 2), Issue(ownerUserId: 2, due: Today.AddDays(-1)), Issue(OpenIssueStatus.InProgress, ownerUserId: 2),
            Issue(ownerUserId: 1),
            Issue(ownerName: "Müşteri BT"), Issue(ownerName: "  Müşteri BT  "),
            Issue(OpenIssueStatus.Closed, ownerUserId: 1, closedAt: new DateTime(2026, 1, 1)),
        ], names, Today);

        Assert.Equal(
            [
                new OwnerWorkloadDto(2, "Ayşe Demir", Open: 3, Overdue: 1),
                new OwnerWorkloadDto(null, "Müşteri BT", Open: 2, Overdue: 0),
                new OwnerWorkloadDto(1, "Elif Yılmaz", Open: 1, Overdue: 0),
            ],
            workload);
    }

    [Fact]
    public void Workload_SameCount_MoreOverdueFirstThenByName()
    {
        var names = new Dictionary<int, string> { [1] = "Zeynep Arslan", [2] = "Can Öztürk", [3] = "Burak Şahin" };

        var workload = ProjectSummaryRules.Workload(
        [
            Issue(ownerUserId: 1, due: Today.AddDays(-2)),
            Issue(ownerUserId: 2),
            Issue(ownerUserId: 3),
        ], names, Today);

        Assert.Equal(["Zeynep Arslan", "Burak Şahin", "Can Öztürk"], workload.Select(w => w.Name));
    }

    [Fact]
    public void Workload_OnlyClosedIssues_IsEmpty()
    {
        Assert.Empty(ProjectSummaryRules.Workload(
            [Issue(OpenIssueStatus.Closed, closedAt: new DateTime(2026, 1, 1))], new Dictionary<int, string>(), Today));
    }
}
