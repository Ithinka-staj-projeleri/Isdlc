using System.Text.RegularExpressions;

namespace Isdlc.Api.Common;

/// <summary>
/// Converts route tokens such as [controller] to kebab-case: "OpenIssues" -> "open-issues".
/// </summary>
public partial class KebabCaseParameterTransformer : IOutboundParameterTransformer
{
    public string? TransformOutbound(object? value) =>
        value is null ? null : WordBoundary().Replace(value.ToString()!, "$1-$2").ToLowerInvariant();

    [GeneratedRegex("([a-z0-9])([A-Z])")]
    private static partial Regex WordBoundary();
}
