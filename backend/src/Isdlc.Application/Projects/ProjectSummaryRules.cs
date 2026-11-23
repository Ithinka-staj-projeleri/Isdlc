using Isdlc.Application.OpenIssues;
using Isdlc.Application.Projects.Dtos;
using Isdlc.Application.Risks;
using Isdlc.Domain.Documents;
using Isdlc.Domain.OpenIssues;
using Isdlc.Domain.Requirements;
using Isdlc.Domain.Risks;

namespace Isdlc.Application.Projects;

/// <summary>
/// The project overview's counts (Faz 5 · F1), from one row per record. Pure rules with no database access.
/// Contract: docs/projects-api.md → "Proje özeti".
/// </summary>
public static class ProjectSummaryRules
{
    /// <summary>"Son 30 günde kapanan": closed on or after this many days before today.</summary>
    public const int RecentlyClosedDays = 30;

    public static OpenIssueSummaryDto OpenIssues(IReadOnlyCollection<SummaryOpenIssueRow> issues, DateOnly today)
    {
        var notClosed = issues.Where(i => i.Status != OpenIssueStatus.Closed).ToList();
        var since = today.AddDays(-RecentlyClosedDays).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        return new OpenIssueSummaryDto(
            Open: issues.Count(i => i.Status == OpenIssueStatus.Open),
            InProgress: issues.Count(i => i.Status == OpenIssueStatus.InProgress),
            Closed: issues.Count(i => i.Status == OpenIssueStatus.Closed),
            Overdue: issues.Count(i => OpenIssueRules.IsOverdue(i.Status, i.DueDate, today)),
            ClosedLast30Days: issues.Count(i => i.Status == OpenIssueStatus.Closed && i.ClosedAt >= since),
            ByType: Enum.GetValues<OpenIssueType>()
                .Select(type => new TypeCountDto(type, notClosed.Count(i => i.Type == type)))
                .ToList());
    }

    /// <summary>Open and monitored risks count by severity (probability × impact); closed ones only in total.</summary>
    public static RiskSummaryDto Risks(IReadOnlyCollection<SummaryRiskRow> risks)
    {
        var followed = risks
            .Where(r => r.Status != RiskStatus.Closed)
            .Select(r => RiskRules.SeverityOf(RiskRules.Score(r.Probability, r.Impact)))
            .ToList();

        return new RiskSummaryDto(
            High: followed.Count(s => s == RiskSeverity.High),
            Medium: followed.Count(s => s == RiskSeverity.Medium),
            Low: followed.Count(s => s == RiskSeverity.Low),
            Closed: risks.Count(r => r.Status == RiskStatus.Closed));
    }

    public static RequirementSummaryDto Requirements(IReadOnlyCollection<RequirementStatus> statuses) =>
        new(
            Draft: statuses.Count(s => s == RequirementStatus.Draft),
            InReview: statuses.Count(s => s == RequirementStatus.InReview),
            Approved: statuses.Count(s => s == RequirementStatus.Approved),
            Cancelled: statuses.Count(s => s == RequirementStatus.Cancelled));

    /// <summary>
    /// Every document type is followed on a project unless its document is marked "Gerekli değil"; a type with no saved
    /// document yet is followed and not started.
    /// </summary>
    public static DocumentSummaryDto Documents(int documentTypeCount, IReadOnlyCollection<SummaryDocumentRow> saved) =>
        new(
            Approved: saved.Count(d => d.Status == DocumentStatus.Approved),
            Tracked: documentTypeCount - saved.Count(d => d.Status == DocumentStatus.NotRequired));

    /// <summary>
    /// Not-closed open issues per owner, the busiest first (then the most overdue, then by name). Team members are told
    /// apart by user id and named with userNames; someone outside the system by the name written on the record.
    /// </summary>
    public static IReadOnlyList<OwnerWorkloadDto> Workload(
        IReadOnlyCollection<SummaryOpenIssueRow> issues, IReadOnlyDictionary<int, string> userNames, DateOnly today) =>
        issues
            .Where(i => i.Status != OpenIssueStatus.Closed)
            .GroupBy(i => (i.OwnerUserId, Name: i.OwnerUserId is { } id
                ? userNames.GetValueOrDefault(id, "Bilinmeyen kullanıcı")
                : (i.OwnerName ?? "Sorumlu yok").Trim()))
            .Select(g => new OwnerWorkloadDto(
                g.Key.OwnerUserId,
                g.Key.Name,
                g.Count(),
                g.Count(i => OpenIssueRules.IsOverdue(i.Status, i.DueDate, today))))
            .OrderByDescending(w => w.Open)
            .ThenByDescending(w => w.Overdue)
            .ThenBy(w => w.Name, StringComparer.Create(new System.Globalization.CultureInfo("tr-TR"), ignoreCase: true))
            .ToList();
}
