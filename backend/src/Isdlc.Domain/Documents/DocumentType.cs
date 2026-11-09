using Isdlc.Domain.Process;

namespace Isdlc.Domain.Documents;

/// <summary>
/// A document tracked on every project ("Proje Kapsam Dokümanı"). Master data loaded from the seed
/// (document-types.json); not edited by users and not audited.
/// </summary>
public class DocumentType
{
    public int Id { get; set; }

    /// <summary>"PKD", unique.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>The stage in which the document is first prepared.</summary>
    public int ProcessStageId { get; set; }
    public ProcessStage ProcessStage { get; set; } = null!;

    /// <summary>Order within the stage.</summary>
    public int Order { get; set; }
}
