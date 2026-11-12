using System.Globalization;

namespace Isdlc.Application.Projects;

/// <summary>
/// Proje No shown to users: a plain sequence number kept as text ("14"). It never restarts.
/// Pure rules with no database access. Contract: docs/projects-api.md → "Proje No".
/// </summary>
public static class ProjectCodes
{
    public static string Format(int number) => number.ToString(CultureInfo.InvariantCulture);

    /// <summary>The number after the highest Proje No; 1 when there is none yet.</summary>
    public static int NextNumber(string? highestCode) =>
        int.TryParse(highestCode, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? number + 1
            : 1;
}
