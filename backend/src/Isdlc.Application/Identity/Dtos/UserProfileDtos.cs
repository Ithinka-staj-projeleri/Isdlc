using System.ComponentModel.DataAnnotations;
using Isdlc.Application.Dashboard.Dtos;
using Isdlc.Domain.Identity;

namespace Isdlc.Application.Identity.Dtos;

/// <summary>GET /api/v1/users/{id}/profile (Faz 5 · F9). Contract: docs/users-api.md</summary>
public record UserProfileDto(
    int Id,
    string FullName,
    string Email,
    IReadOnlyList<Permission> Permissions, // Faz 7 · A2: in place of the title
    bool IsActive,
    string? Expertise,
    string? WorkingNote,
    UserWorkloadCountsDto? Workload,                      // null for an inactive user (P4)
    DashboardListDto<DashboardOpenIssueDto> OpenIssues,   // the dashboard's row shape (P5)
    DashboardListDto<WaitingStepDto> WaitingSteps);       // empty for an inactive user (P6)

/// <summary>The person's row of the team workload (ITeamWorkloadService), without the name and permissions.</summary>
public record UserWorkloadCountsDto(int OpenIssues, int OverdueOpenIssues, int WaitingSteps);

/// <summary>PUT /api/v1/users/me/profile: the signed-in user's own expertise and note (P2). Empty text is saved as null.</summary>
public class UpdateMyProfileDto
{
    [MaxLength(UserProfileLimits.Expertise)]
    public string? Expertise { get; set; }

    [MaxLength(UserProfileLimits.WorkingNote)]
    public string? WorkingNote { get; set; }
}

/// <summary>GET /api/v1/users/workload: an active user to pick as an owner, with their expertise and load (P7).</summary>
public record UserWorkloadDto(
    int Id,
    string FullName,
    IReadOnlyList<Permission> Permissions,
    string? Expertise,
    int OpenIssues,
    int OverdueOpenIssues,
    int WaitingSteps);
