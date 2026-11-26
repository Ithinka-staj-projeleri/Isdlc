namespace Isdlc.Domain.Process;

/// <summary>
/// Work that must exist on the project before the step can be completed or answered "Yes" (Faz 7 · A4).
/// Kept as a JSON list on the step (ProcessStep.Prerequisites). Rules: docs/configurability.md → Adım ön koşulları.
/// </summary>
public record StepPrerequisite(PrerequisiteKind Kind, string? Value = null)
{
    public const int MaxValueLength = 50;
}

public enum PrerequisiteKind
{
    /// <summary>The Talep Kayıt Formu's required fields are filled.</summary>
    RequestFormComplete,

    /// <summary>The project's form of the question set in Value (e.g. "customer-fit") is complete.</summary>
    QuestionnaireComplete,

    /// <summary>A level has been applied to the project.</summary>
    LevelApplied,

    /// <summary>The project has at least one meeting minutes.</summary>
    MeetingExists,

    /// <summary>The project has at least one risk.</summary>
    RiskExists,

    /// <summary>The project has requirements and none of them is a draft or in review.</summary>
    RequirementsApproved,

    /// <summary>The project's document of the type in Value is approved or not required.</summary>
    DocumentApproved,
}
