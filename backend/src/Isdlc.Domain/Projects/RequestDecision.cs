using Isdlc.Domain.Common;
using Isdlc.Domain.Process;

namespace Isdlc.Domain.Projects;

/// <summary>
/// A filled-in Talep Ret / Erteleme Formu (ISDLC-Form-Analiz-007): why a request was rejected or postponed.
/// Written together with the StepRecord of the decision and never edited. A request can collect several
/// (postponed, taken back, then rejected). Contract: docs/process-api.md → "Ret / erteleme kararı".
/// </summary>
public class RequestDecision : AuditableEntity, IAudited, IProjectRecord
{
    public int Id { get; set; }

    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    /// <summary>The process record the decision belongs to (Rejected, Postponed, or the closing "No").</summary>
    public int StepRecordId { get; set; }
    public StepRecord StepRecord { get; set; } = null!;

    public RequestDecisionKind Decision { get; set; }

    public DateOnly DecisionDate { get; set; }

    /// <summary>Kararı veren: a person or a board, as written on the form. Not a system user.</summary>
    public string DecidedBy { get; set; } = string.Empty;

    /// <summary>Gerekçe kategorileri: one or more of the form's ten options.</summary>
    public List<DecisionReason> Reasons { get; set; } = [];

    /// <summary>What "Diğer" means; only when Reasons contains Other.</summary>
    public string? ReasonOther { get; set; }

    /// <summary>Gerekçe açıklaması.</summary>
    public string Explanation { get; set; } = string.Empty;

    /// <summary>Yeniden değerlendirme tarihi: only for a postponement.</summary>
    public DateOnly? ReevaluationDate { get; set; }
}

public enum RequestDecisionKind
{
    Reject,
    Postpone,
}

/// <summary>The reason categories of the Talep Ret / Erteleme Formu, in its order.</summary>
public enum DecisionReason
{
    StrategicMisfit,
    InsufficientValue,
    BudgetConstraint,
    CapacityShortage,
    TechnicalInfeasibility,
    LegalNonCompliance,
    UnacceptableRisk,
    Prioritization,
    InsufficientInformation,
    Other,
}
