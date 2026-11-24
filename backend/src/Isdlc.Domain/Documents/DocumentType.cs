using Isdlc.Domain.Common;
using Isdlc.Domain.Process;

namespace Isdlc.Domain.Documents;

/// <summary>
/// A document tracked on every project ("Proje Kapsam Dokümanı"). Master data loaded from the seed
/// (document-types.json); not edited by users and not audited.
/// </summary>
public class DocumentType : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }

    /// <summary>"PKD", unique.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>The template (Seed/ file code) this copy came from; empty for a record the company added itself.</summary>
    public string? TemplateCode { get; set; }

    /// <summary>The company changed this copy; the startup sync no longer follows the template (docs/multi-tenancy.md → Seed).</summary>
    public bool IsCustomized { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>The stage in which the document is first prepared.</summary>
    public int ProcessStageId { get; set; }
    public ProcessStage ProcessStage { get; set; } = null!;

    /// <summary>Order within the stage.</summary>
    public int Order { get; set; }
}
