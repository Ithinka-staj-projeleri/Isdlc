using Isdlc.Application.OpenIssues;
using Microsoft.EntityFrameworkCore;

namespace Isdlc.Application.Common;

/// <summary>
/// Codes of project records ("AK-001", "TT-001"): increasing within the project (docs/data-model.md → "Proje kayıtları").
/// </summary>
public static class RecordCodes
{
    /// <summary>The number after the highest code in the given codes of one project; 1 when there is none.</summary>
    public static async Task<int> NextNumberAsync(this IQueryable<string> projectCodes, CancellationToken cancellationToken)
    {
        // Longer first, so "AK-1000" counts as higher than "AK-999".
        var highestCode = await projectCodes
            .OrderByDescending(code => code.Length)
            .ThenByDescending(code => code)
            .FirstOrDefaultAsync(cancellationToken);

        return OpenIssueRules.NextCodeNumber(highestCode);
    }
}
