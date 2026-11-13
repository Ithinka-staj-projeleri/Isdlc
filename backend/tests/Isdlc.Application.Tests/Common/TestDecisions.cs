using Isdlc.Application.Process.Dtos;
using Isdlc.Domain.Projects;

namespace Isdlc.Application.Tests.Common;

/// <summary>A filled-in Talep Ret / Erteleme Formu for tests; every test changes only what it checks.</summary>
public static class TestDecisions
{
    public static readonly DateOnly DecisionDate = new(2026, 11, 13);

    public static RequestDecisionFormDto Form(string explanation = "Ekip kapasitesi yıl sonuna kadar dolu.") => new()
    {
        DecisionDate = DecisionDate,
        DecidedBy = "Yazılım Departman Yöneticisi",
        Reasons = [DecisionReason.CapacityShortage],
        Explanation = explanation,
    };
}
