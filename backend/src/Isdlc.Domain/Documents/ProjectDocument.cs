using Isdlc.Domain.Common;
using Isdlc.Domain.Projects;

namespace Isdlc.Domain.Documents;

/// <summary>
/// The state of one document in one project. Created the first time the document is saved on the project;
/// until then the document counts as NotStarted. Rules: docs/data-model.md → ProjectDocument
/// </summary>
public class ProjectDocument : AuditableEntity, IAudited, IProjectRecord
{
    public int Id { get; set; }

    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public int DocumentTypeId { get; set; }
    public DocumentType DocumentType { get; set; } = null!;

    public DocumentStatus Status { get; set; } = DocumentStatus.NotStarted;
    public string? Note { get; set; }

    /// <summary>Where the document is kept (SharePoint, Teams...). Files are not uploaded.</summary>
    public string? Link { get; set; }
}
