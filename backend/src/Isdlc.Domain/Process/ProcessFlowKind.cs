namespace Isdlc.Domain.Process;

/// <summary>What a flow's records are; decides the menu that lists them.</summary>
public enum ProcessFlowKind
{
    /// <summary>Requests and projects (Talepler, Projeler).</summary>
    Request,

    /// <summary>Process improvement proposals (Süreç İyileştirme).</summary>
    Improvement,

    /// <summary>Weekly resource planning (Kaynak Planlama).</summary>
    ResourcePlanning,

    /// <summary>Monthly billing (Faturalama).</summary>
    Billing,
}
