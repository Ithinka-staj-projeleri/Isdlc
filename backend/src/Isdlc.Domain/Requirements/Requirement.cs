using Isdlc.Domain.Common;
using Isdlc.Domain.Projects;

namespace Isdlc.Domain.Requirements;

/// <summary>
/// A requirement of the project. A User Story is not a separate record: it is written into the description.
/// Never deleted; cancelled instead. Rules: docs/data-model.md → Requirement, docs/requirements-documents-api.md
/// </summary>
public class Requirement : AuditableEntity, IAudited, IProjectRecord
{
    public int Id { get; set; }

    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    /// <summary>"GR-001", increasing within the project.</summary>
    public string Code { get; set; } = string.Empty;

    public RequirementType Type { get; set; }
    public string Title { get; set; } = string.Empty;

    /// <summary>Free text; a User Story goes here ("... olarak, ... istiyorum, böylece ...").</summary>
    public string? Description { get; set; }

    public string? AcceptanceCriteria { get; set; }

    public RequirementPriority Priority { get; set; }
    public RequirementStatus Status { get; set; } = RequirementStatus.Draft;
}
