using System.Text.RegularExpressions;

namespace Isdlc.Application.Tenants;

/// <summary>Rules of a company's own fields. Pure: no database.</summary>
public static partial class TenantCodeRules
{
    /// <summary>Lower-case letters, digits and single hyphens between them; 2–50 characters ("ornek-lojistik").</summary>
    public static bool IsValid(string code) => code.Length is >= 2 and <= 50 && CodePattern().IsMatch(code);

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex CodePattern();
}
