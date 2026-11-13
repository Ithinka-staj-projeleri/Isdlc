using System.Globalization;

namespace Isdlc.Application.Projects;

/// <summary>
/// Codes shown to users (docs/projects-api.md → "Proje No"). Pure rules with no database access.
/// A project's Proje No is a plain sequence number kept as text ("14"); it never restarts.
/// An add-on (ek geliştirme) shows its project's number and its own number within that project ("14 / EG-01").
/// </summary>
public static class ProjectCodes
{
    private const string AddOnMark = "EG-";

    public static string Format(int number) => number.ToString(CultureInfo.InvariantCulture);

    /// <summary>The number after the highest Proje No; 1 when there is none yet.</summary>
    public static int NextNumber(string? highestCode) =>
        int.TryParse(highestCode, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? number + 1
            : 1;

    /// <summary>"14 / EG-01": padded to 2 digits, keeps growing after 99.</summary>
    public static string FormatAddOn(string parentCode, int number) =>
        $"{parentCode} / {AddOnMark}{number.ToString("D2", CultureInfo.InvariantCulture)}";

    /// <summary>The number after the project's highest add-on code; 1 for its first add-on.</summary>
    public static int NextAddOnNumber(string? highestAddOnCode)
    {
        var mark = highestAddOnCode?.LastIndexOf(AddOnMark, StringComparison.Ordinal) ?? -1;
        return mark >= 0
            && int.TryParse(highestAddOnCode![(mark + AddOnMark.Length)..], NumberStyles.None,
                CultureInfo.InvariantCulture, out var number)
            ? number + 1
            : 1;
    }
}
