namespace Isdlc.Application.Projects;

/// <summary>
/// Project codes shown to users: "ISDLC-2026-019" = year the request was created + number within that year.
/// Pure rules with no database access. Contract: docs/projects-api.md → "Proje kodu".
/// </summary>
public static class ProjectCodes
{
    /// <summary>"ISDLC-2026-": every code of that year starts with it.</summary>
    public static string YearPrefix(int year) => $"ISDLC-{year}-";

    /// <summary>The number is padded to 3 digits and keeps growing after 999.</summary>
    public static string Format(int year, int number) => $"{YearPrefix(year)}{number:D3}";

    /// <summary>The number after the highest code of the year; 1 when the year has none yet.</summary>
    public static int NextNumber(string? highestCodeOfYear) =>
        highestCodeOfYear is not null
        && int.TryParse(highestCodeOfYear[(highestCodeOfYear.LastIndexOf('-') + 1)..], out var number)
            ? number + 1
            : 1;
}
