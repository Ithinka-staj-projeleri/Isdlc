using Isdlc.Application.Common.Models;
using Isdlc.Domain.Identity;
using Isdlc.Domain.OpenIssues;
using Isdlc.Domain.Projects;

namespace Isdlc.Application.Dashboard.Dtos;

/// <summary>
/// One team member's load (Faz 5 · F2): open issues they own and process steps waiting for them, on active records.
/// Shared with the person profile and the owner suggestion (F9). Contract: docs/dashboard-api.md → "Ekip iş yükü".
/// </summary>
public record TeamMemberWorkloadDto(
    UserSummaryDto User,
    IReadOnlyList<Permission> Permissions, // what the user can do in the flow (Faz 7 · A2), in the list's order
    int OpenIssues,         // not closed, owned by the user
    int OverdueOpenIssues,  // of those, past the due date
    int WaitingSteps);      // current steps the user can act on (the "Bana bekleyen adımlar" rule)

// ---- What the rules count (one row per record) ----

public record WorkloadUserRow(int Id, string FullName, IReadOnlyList<Permission> Permissions);

public record WorkloadOpenIssueRow(int OwnerUserId, OpenIssueStatus Status, DateOnly? DueDate);

/// <summary>An active record's current step: who can act on it, and who is assigned to the record.</summary>
public record WorkloadStepRow(
    Permission ResponsiblePermission, Permission? ApproverPermission, int? BusinessAnalystId, int? ProjectManagerId);
