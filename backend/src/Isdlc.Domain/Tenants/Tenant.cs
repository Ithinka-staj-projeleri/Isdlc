namespace Isdlc.Domain.Tenants;

/// <summary>
/// A company using the application: its own users, flows, question sets, document types and records.
/// Never deleted, only deactivated. Not tenant-owned itself and not audited. Rules: docs/multi-tenancy.md → Tenant.
/// </summary>
public class Tenant
{
    public const int CodeMaxLength = 50;
    public const int NameMaxLength = 200;

    /// <summary>The company that existed before Phase 6; every record of that time was moved to it.</summary>
    public const int IthinkaId = 1;

    public int Id { get; set; }

    /// <summary>Short name: lower-case letters, digits and hyphens ("ithinka"). Unique; does not change.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Users of an inactive company cannot sign in.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>AI assistants on for this company.</summary>
    public bool AiEnabled { get; set; }

    /// <summary>Text may be sent to the outside model provider.</summary>
    public bool AiDataSharingAllowed { get; set; }

    /// <summary>The model the company chose (Faz 7 · A9); empty: the AI service's default.</summary>
    public string? AiModel { get; set; }

    /// <summary>Input + output tokens a month (UTC calendar month); empty: no limit.</summary>
    public int? AiMonthlyTokenLimit { get; set; }

    public DateTime CreatedAt { get; set; }
}
