using Isdlc.Domain.Common;
using Isdlc.Domain.Identity;
using Isdlc.Domain.Projects;

namespace Isdlc.Domain.Risks;

/// <summary>
/// A risk of the project (Risk Analysis Form, flow 4.8). The score (Probability × Impact) and its severity are not
/// stored; they are calculated when read. Never deleted; closed instead.
/// Rules: docs/data-model.md → Risk, docs/risks-api.md
/// </summary>
public class Risk : AuditableEntity, IAudited, IProjectRecord
{
    public int Id { get; set; }

    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    /// <summary>"RSK-001", increasing within the project.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>What the risk is ("ERP servis dokümantasyonu eksik").</summary>
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public RiskCategory Category { get; set; }

    /// <summary>1–5.</summary>
    public int Probability { get; set; }

    /// <summary>1–5.</summary>
    public int Impact { get; set; }

    /// <summary>The preventive action.</summary>
    public string? Mitigation { get; set; }

    /// <summary>The team member responsible for the risk; must be an active user.</summary>
    public int OwnerUserId { get; set; }
    public User OwnerUser { get; set; } = null!;

    /// <summary>Shown to the requester ("Paylaşılır") or kept inside the team ("Dahili").</summary>
    public bool IsSharedWithRequester { get; set; }

    public RiskStatus Status { get; set; } = RiskStatus.Open;
}
