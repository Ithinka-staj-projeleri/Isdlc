namespace Isdlc.Domain.Risks;

public enum RiskCategory
{
    Technical,
    Scope,
    Stakeholder,
    Schedule,
    Cost,
    Data,
    /// <summary>Güvenlik / KVKK.</summary>
    SecurityPrivacy,
    Supplier,
}

public enum RiskStatus
{
    Open,
    Monitoring,
    Closed,
}

/// <summary>
/// Derived from the score, never stored: 1–7 Low, 8–14 Medium, 15–25 High (docs/risks-api.md → "Skor ve seviye").
/// </summary>
public enum RiskSeverity
{
    Low,
    Medium,
    High,
}
