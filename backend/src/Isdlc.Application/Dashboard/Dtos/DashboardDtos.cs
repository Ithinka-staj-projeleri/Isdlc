using Isdlc.Application.Common.Models;
using Isdlc.Application.OpenIssues.Dtos;
using Isdlc.Domain.Assessment;
using Isdlc.Domain.OpenIssues;
using Isdlc.Domain.Process;
using Isdlc.Domain.Risks;

namespace Isdlc.Application.Dashboard.Dtos;

/// <summary>Which projects the general sections count: all of them, or only the user's (analyst or project manager).</summary>
public enum DashboardScope
{
    All,
    Mine,
}

/// <summary>GET /api/v1/dashboard. Contract: docs/dashboard-api.md</summary>
public record DashboardDto(
    DashboardScope Scope,
    DashboardSummaryDto Summary,
    IReadOnlyList<StageCountDto> Stages,
    DashboardListDto<WaitingStepDto> MyWaitingSteps,
    DashboardListDto<DashboardOpenIssueDto> MyOpenIssues,
    DashboardListDto<DashboardOpenIssueDto> OverdueOpenIssues,
    IReadOnlyList<RecentActivityDto> RecentActivity,
    DashboardListDto<HighRiskDto> HighRisks,
    LevelCountsDto Levels,
    DashboardListDto<AcceptanceQueueItemDto> AcceptanceQueue);

public record DashboardSummaryDto(
    int WaitingRequests,
    int ActiveProjects,
    int MyWaitingSteps,
    int OverdueOpenIssues,
    int OverdueProjects,
    FinishedThisYearDto FinishedThisYear,
    int HighRisks);

/// <summary>Records that finished this year, by their final status.</summary>
public record FinishedThisYearDto(int Closed, int Lost, int Rejected, int Redirected);

/// <summary>Active records waiting in a stage; all 13 stages are returned, empty ones too.</summary>
public record StageCountDto(int StageOrder, string ShortTitle, int Count);

/// <summary>The first rows of a list (at most 10) and how many there are in total.</summary>
public record DashboardListDto<T>(int TotalCount, IReadOnlyList<T> Items);

public record DashboardProjectDto(int Id, string Code, string Name);

public record WaitingStepRefDto(int Id, string Code, int StageOrder, string StageShortTitle);

/// <summary>A step the signed-in user can act on now.</summary>
public record WaitingStepDto(
    DashboardProjectDto Project,
    WaitingStepRefDto Step,
    bool IsApproval,
    bool IsDecision,
    DateTime WaitingSince,
    int WaitingDays);

public record DashboardOpenIssueDto(
    DashboardProjectDto Project,
    int Id,
    string Code,
    OpenIssueType Type,
    string Title,
    OwnerDto Owner,
    DateOnly? DueDate,
    OpenIssueStatus Status,
    bool IsOverdue);

/// <summary>A risk that is not closed and whose score is high (15 or more, RiskRules).</summary>
public record HighRiskDto(
    DashboardProjectDto Project,
    int Id,
    string Code,
    string Title,
    int Probability,
    int Impact,
    int Score,
    RiskStatus Status,
    UserSummaryDto Owner);

/// <summary>Active records by applied level (Project.Level); NotSet: no level applied yet (step 2.4).</summary>
public record LevelCountsDto(int Level1, int Level2, int Level3, int NotSet);

/// <summary>
/// An active request at step 3.1 or 3.2 and its acceptance scoring. WeightedScore and Recommendation are null
/// until all six criteria are scored (AssessmentRules).
/// </summary>
public record AcceptanceQueueItemDto(
    DashboardProjectDto Project,
    string StepCode,
    decimal? WeightedScore,
    AcceptanceRecommendation? Recommendation,
    int AnsweredCount,
    DateTime WaitingSince);

public record RecentActivityDto(
    DashboardProjectDto Project,
    string StepCode,
    StepAction Action,
    UserSummaryDto PerformedBy,
    DateTime PerformedAt);
