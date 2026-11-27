namespace Isdlc.Domain.Process;

/// <summary>
/// Which records a flow starts (Faz 7 · A7): at most one flow of a company for each value. The company chooses it;
/// a flow without it starts nothing (a draft, or an old flow kept for its records). Replaces finding flows by code.
/// </summary>
public enum ProcessFlowUse
{
    /// <summary>Talepler → Yeni talep.</summary>
    NewRequest,

    /// <summary>An add-on (ek geliştirme) on a project.</summary>
    AdditionalModule,

    /// <summary>A process improvement proposal (Süreç İyileştirme).</summary>
    Improvement,

    /// <summary>A week of resource planning.</summary>
    ResourcePlanning,

    /// <summary>A month of billing.</summary>
    Billing,
}
