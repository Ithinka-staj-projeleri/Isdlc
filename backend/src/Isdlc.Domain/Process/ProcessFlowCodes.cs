namespace Isdlc.Domain.Process;

/// <summary>Codes of the flows that code opens records in. Flows themselves come from Seed/flows/*.json.</summary>
public static class ProcessFlowCodes
{
    /// <summary>The flow a new request (Talepler → Yeni talep) follows.</summary>
    public const string NewRequest = "new-software-project";

    /// <summary>An add-on or extra module for an ongoing project (Faz 4, D3).</summary>
    public const string AddOn = "add-on";

    /// <summary>A process improvement proposal (Faz 4, E5).</summary>
    public const string ProcessImprovement = "process-improvement";

    /// <summary>One week of resource planning (Faz 4, B6).</summary>
    public const string ResourcePlanning = "resource-planning";

    /// <summary>One month of billing (Faz 4, B7).</summary>
    public const string Billing = "billing";
}
