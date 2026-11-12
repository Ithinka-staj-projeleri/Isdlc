using Isdlc.Domain.Common;
using Isdlc.Domain.Projects;

namespace Isdlc.Domain.ProcessImprovements;

/// <summary>
/// What a process improvement proposal has beyond its Project record (which follows the process-improvement flow:
/// code "SI-001", title in Name, the problem in Description, who runs it in BusinessAnalystId).
/// One row per proposal. Fields are grouped by the company forms (040–042); fields those forms add later go to their group.
/// Contract: docs/process-improvement-api.md
/// </summary>
public class ProcessImprovement : AuditableEntity, IAudited, IProjectRecord
{
    public const int MinScore = 1;
    public const int MaxScore = 5;
    public const int MaxShortText = 200;
    public const int MaxProposedSolution = 2000;
    public const int MaxStudyText = 4000;
    public const int MaxPilotMeasure = 1000;
    public const int MaxPilotFeedback = 4000;
    public const int MaxPilotDecision = 2000;

    public int Id { get; set; }

    /// <summary>The proposal's own record; unique.</summary>
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    // ---- 1. Proposal (Kayıt Listesi, step 1) ----

    public ImprovementSource Source { get; set; }
    public string AffectedProcess { get; set; } = string.Empty;
    public string? ProposedSolution { get; set; }

    /// <summary>Anyone may bring a proposal, so this is a name, not a user.</summary>
    public string SuggestedBy { get; set; } = string.Empty;

    // ---- 2. Prioritization (step 2): 1–5 each, all three or none. The priority is worked out, not stored. ----

    public int? Impact { get; set; }
    public int? Frequency { get; set; }
    public int? Ease { get; set; }

    // ---- 3. Study (Çalışma Dokümanı, steps 3–4) ----

    public string? CurrentState { get; set; }
    public string? RootCause { get; set; }
    public string? Solution { get; set; }
    public string? PilotPlan { get; set; }
    public string? PilotTarget { get; set; }
    public string? CostBenefit { get; set; }

    // ---- 4. Pilot evaluation (Pilot Değerlendirme Raporu, step 7); a repeated pilot overwrites, history is in the audit log ----

    public string? PilotActual { get; set; }
    public string? PilotFeedback { get; set; }
    public PilotResult? PilotResult { get; set; }
    public string? PilotDecision { get; set; }
}
