using Isdlc.Domain.Common;
using Isdlc.Domain.Projects;

namespace Isdlc.Domain.Assessment;

/// <summary>
/// The project's level questions (flow 2.4, 1–3 each) and acceptance criteria (flow 3.1, 1–5 each); one per project.
/// Null means "not answered yet". Score, suggested level, weighted score and recommendation are calculated, never stored.
/// The applied level is Project.Level. Rules: docs/data-model.md → ProjectAssessment, docs/assessment-api.md
/// </summary>
public class ProjectAssessment : AuditableEntity, IAudited, IProjectRecord
{
    public int Id { get; set; }

    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    // ---- Level questions, 1–3 ----

    public int? Effort { get; set; }
    public int? ScreensAndModules { get; set; }
    public int? Integrations { get; set; }
    public int? UserRoles { get; set; }
    public int? DataSensitivity { get; set; }
    public int? HardwareDependency { get; set; }

    // ---- Acceptance criteria, 1–5 ----

    public int? StrategicFit { get; set; }
    public int? CommercialValue { get; set; }
    public int? TechnicalFeasibility { get; set; }
    public int? ResourceAndSchedule { get; set; }
    public int? CustomerFit { get; set; }

    /// <summary>A high score means a low risk.</summary>
    public int? RiskLevel { get; set; }
}
