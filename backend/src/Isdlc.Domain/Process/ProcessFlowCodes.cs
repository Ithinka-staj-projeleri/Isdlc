namespace Isdlc.Domain.Process;

/// <summary>Codes of the flows that code opens records in. Flows themselves come from Seed/flows/*.json.</summary>
public static class ProcessFlowCodes
{
    /// <summary>
    /// The flow a new request (Talepler → Yeni talep) follows.
    /// Until the company's five flows are seeded (Faz 4, E2) this is the 13-stage ISDLC v12 flow.
    /// </summary>
    public const string NewRequest = "isdlc-v12";
}
