namespace Isdlc.Domain.Assessment;

/// <summary>
/// What the acceptance scores suggest; never stored. The decision is the department manager's at step 3.2.
/// ≥ 3.50 Accept, 2.50–3.49 Conditional, &lt; 2.50 RejectOrPostpone (docs/assessment-api.md).
/// </summary>
public enum AcceptanceRecommendation
{
    Accept,
    Conditional,
    RejectOrPostpone,
}
