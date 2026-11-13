namespace Isdlc.Domain.ProcessImprovements;

/// <summary>Where a proposal comes from (the process improvement flow, step 1).</summary>
public enum ImprovementSource
{
    /// <summary>Proje kapanışı / retrospektif</summary>
    Retrospective,

    /// <summary>Hata, sapma, müşteri şikâyeti</summary>
    DefectOrComplaint,

    /// <summary>Çalışan önerisi</summary>
    EmployeeSuggestion,

    /// <summary>Yönetim hedefi</summary>
    ManagementGoal,
}

/// <summary>Worked out from impact + frequency + ease; never stored.</summary>
public enum ImprovementPriority
{
    Low,
    Medium,
    High,
}

/// <summary>How the pilot went (Pilot Değerlendirme Raporu). The flow's branch is still the answer to step 7.</summary>
public enum PilotResult
{
    Met,
    PartiallyMet,
    NotMet,
}
