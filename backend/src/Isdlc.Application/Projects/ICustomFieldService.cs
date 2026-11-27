using Isdlc.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Isdlc.Application.Projects.Dtos;

namespace Isdlc.Application.Projects;

/// <summary>
/// The company's own request form fields (Faz 7 · A6). Everyone reads the active ones; only the Şirket Yöneticisi adds,
/// changes and deactivates them. Contract: docs/configurability.md → Şirkete özel alanlar.
/// </summary>
public interface ICustomFieldService
{
    /// <summary>Active fields by Order; includeInactive (company administrators only, else 403) adds the inactive ones.</summary>
    Task<IReadOnlyList<CustomFieldDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken);

    /// <summary>409 when the company already has the key.</summary>
    Task<CustomFieldDto> CreateAsync(CreateCustomFieldDto request, CancellationToken cancellationToken);

    /// <summary>Key and Type never change. 409 when an option a skip rule uses is removed.</summary>
    Task<CustomFieldDto> UpdateAsync(int id, UpdateCustomFieldDto request, CancellationToken cancellationToken);
}

/// <summary>Reading the field definitions where values are checked (request form, prerequisites).</summary>
public static class CustomFieldQueries
{
    /// <summary>The company's active fields in form order, as CustomValueRules needs them.</summary>
    public static async Task<IReadOnlyList<CustomFieldDefinition>> ActiveDefinitionsAsync(
        this IQueryable<CustomField> fields, CancellationToken cancellationToken)
    {
        var rows = await fields
            .AsNoTracking()
            .Where(f => f.IsActive)
            .OrderBy(f => f.Order)
            .ThenBy(f => f.Id)
            .Select(f => new { f.Key, f.Type, f.Options, f.IsRequired })
            .ToListAsync(cancellationToken);

        return rows.Select(f => new CustomFieldDefinition(f.Key, f.Type, f.Options, f.IsRequired)).ToList();
    }

    /// <summary>The keys of the company's active fields, for the skip rules (ConditionContext.ActiveCustomFields).</summary>
    public static async Task<IReadOnlySet<string>> ActiveKeysAsync(
        this IQueryable<CustomField> fields, CancellationToken cancellationToken) =>
        (await fields.AsNoTracking().Where(f => f.IsActive).Select(f => f.Key).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);
}
