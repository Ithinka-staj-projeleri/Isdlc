using Isdlc.Domain.Documents;
using Isdlc.Domain.OpenIssues;
using Isdlc.Domain.Requirements;
using Isdlc.Domain.Risks;

namespace Isdlc.Application.Projects.Dtos;

/// <summary>
/// GET /api/v1/projects/{projectId}/summary — the overview on top of the project's "Genel Bilgiler" tab (Faz 5 · F1).
/// Counted from the project's own records; nothing is stored. Contract: docs/projects-api.md → "Proje özeti".
/// </summary>
public record ProjectSummaryDto(
    OpenIssueSummaryDto OpenIssues,
    RiskSummaryDto Risks,
    RequirementSummaryDto Requirements,
    DocumentSummaryDto Documents,
    IReadOnlyList<OwnerWorkloadDto> Workload);

/// <summary>Open issues by status; the not-closed ones also by type.</summary>
public record OpenIssueSummaryDto(
    int Open,
    int InProgress,
    int Closed,
    int Overdue,                                // not closed and past the due date
    int ClosedLast30Days,
    IReadOnlyList<TypeCountDto> ByType);        // not closed, every type, in enum order

public record TypeCountDto(OpenIssueType Type, int Count);

/// <summary>Risks still followed (Open or Monitoring) by severity, and the closed ones.</summary>
public record RiskSummaryDto(int High, int Medium, int Low, int Closed);

/// <summary>Requirements by status.</summary>
public record RequirementSummaryDto(int Draft, int InReview, int Approved, int Cancelled);

/// <summary>Document types followed on the project (not marked "Gerekli değil") and how many of them are approved.</summary>
public record DocumentSummaryDto(int Approved, int Tracked);

/// <summary>
/// Ekip iş yükü: not-closed open issues per owner, most first. A team member has a user id; someone outside the system
/// (OwnerName) has none.
/// </summary>
public record OwnerWorkloadDto(int? UserId, string Name, int Open, int Overdue);

// ---- What the rules count (one row per record) ----

public record SummaryOpenIssueRow(
    OpenIssueType Type, OpenIssueStatus Status, DateOnly? DueDate, DateTime? ClosedAt, int? OwnerUserId, string? OwnerName);

public record SummaryRiskRow(int Probability, int Impact, RiskStatus Status);

public record SummaryDocumentRow(DocumentStatus Status);
