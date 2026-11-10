using Isdlc.Application.Common.Models;
using Isdlc.Application.OpenIssues.Dtos;
using Isdlc.Domain.OpenIssues;
using Isdlc.Domain.Process;

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
    IReadOnlyList<RecentActivityDto> RecentActivity);

public record DashboardSummaryDto(
    int WaitingRequests,
    int ActiveProjects,
    int MyWaitingSteps,
    int OverdueOpenIssues,
    int OverdueProjects,
    FinishedThisYearDto FinishedThisYear);

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

public record RecentActivityDto(
    DashboardProjectDto Project,
    string StepCode,
    StepAction Action,
    UserSummaryDto PerformedBy,
    DateTime PerformedAt);
