using System.Linq.Expressions;
using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Projects.Dtos;
using Isdlc.Application.Tenants;
using Isdlc.Domain.Process;
using Isdlc.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace Isdlc.Application.Projects;

public class CustomFieldService(IApplicationDbContext dbContext, ICurrentUserService currentUser) : ICustomFieldService
{
    private static readonly Expression<Func<CustomField, CustomFieldDto>> ToDto = f => new CustomFieldDto(
        f.Id, f.Key, f.Label, f.Type, f.Options, f.IsRequired, f.HelpText, f.Order, f.IsActive);

    private static readonly Func<CustomField, CustomFieldDto> Map = ToDto.Compile();

    public async Task<IReadOnlyList<CustomFieldDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        RequireSignedIn();
        if (includeInactive)
        {
            EnsureTenantAdmin();
        }

        return await dbContext.CustomFields
            .AsNoTracking()
            .Where(f => includeInactive || f.IsActive)
            .OrderBy(f => f.Order)
            .ThenBy(f => f.Id)
            .Select(ToDto)
            .ToListAsync(cancellationToken);
    }

    public async Task<CustomFieldDto> CreateAsync(CreateCustomFieldDto request, CancellationToken cancellationToken)
    {
        EnsureTenantAdmin();

        var key = request.Key!.Trim();
        if (await dbContext.CustomFields.AnyAsync(f => f.Key == key, cancellationToken))
        {
            throw new ConflictException($"«{key}» anahtarıyla bir alan zaten var.");
        }

        var field = new CustomField
        {
            Key = key,
            Type = request.Type!.Value,
            Order = request.Order ?? await NextOrderAsync(cancellationToken),
        };
        ApplyForm(field, request);
        dbContext.CustomFields.Add(field);

        // Two administrators adding the same key at once: the unique index refuses the second.
        await dbContext.SaveChangesAsync(cancellationToken);

        return Map(field);
    }

    public async Task<CustomFieldDto> UpdateAsync(int id, UpdateCustomFieldDto request, CancellationToken cancellationToken)
    {
        EnsureTenantAdmin();

        var field = await dbContext.CustomFields.SingleOrDefaultAsync(f => f.Id == id, cancellationToken)
            ?? throw new NotFoundException("Alan bulunamadı.");

        if (CustomFieldFormDto.OptionsError(field.Type, request.Options) is { } optionsError)
        {
            throw new FieldValidationException(new Dictionary<string, string[]> { ["options"] = [optionsError] });
        }

        var newOptions = TrimmedOptions(request.Options);
        var removed = field.Options
            .Where(o => !newOptions.Contains(o, StringComparer.OrdinalIgnoreCase))
            .ToList();
        if (removed.Count > 0)
        {
            await EnsureOptionsNotInRulesAsync(field.Key, removed, cancellationToken);
        }

        ApplyForm(field, request);
        field.Order = request.Order ?? field.Order;
        field.IsActive = request.IsActive!.Value;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Map(field);
    }

    // ---- Helpers ----

    private static void ApplyForm(CustomField field, CustomFieldFormDto request)
    {
        field.Label = request.Label!.Trim();
        field.Options = TrimmedOptions(request.Options);
        field.IsRequired = request.IsRequired;
        field.HelpText = string.IsNullOrWhiteSpace(request.HelpText) ? null : request.HelpText.Trim();
    }

    private static List<string> TrimmedOptions(IEnumerable<string>? options) =>
        (options ?? []).Select(o => o.Trim()).ToList();

    private async Task<int> NextOrderAsync(CancellationToken cancellationToken) =>
        (await dbContext.CustomFields.MaxAsync(f => (int?)f.Order, cancellationToken) ?? 0) + 1;

    /// <summary>
    /// A skip rule that compares the field with a removed option could never hold again (docs/configurability.md →
    /// Şirkete özel alanlar). Rules are a JSON column, so the company's steps are read and checked here.
    /// </summary>
    private async Task EnsureOptionsNotInRulesAsync(
        string key, IReadOnlyList<string> removed, CancellationToken cancellationToken)
    {
        var field = StepConditionRule.CustomPrefix + key;
        var steps = await dbContext.ProcessSteps
            .AsNoTracking()
            .Select(s => new { s.Code, FlowName = s.ProcessFlow.Name, s.Conditions })
            .ToListAsync(cancellationToken);

        foreach (var step in steps)
        {
            var used = step.Conditions.FirstOrDefault(rule => rule.Field == field
                && rule.Value is not null
                && removed.Contains(rule.Value, StringComparer.OrdinalIgnoreCase));
            if (used is not null)
            {
                throw new ConflictException(
                    $"«{used.Value}» seçeneği bir atlama kuralında kullanılıyor: {step.FlowName} · {step.Code}. adım.");
            }
        }
    }

    private int RequireSignedIn() =>
        currentUser.UserId ?? throw new ForbiddenException("İşlem yapmak için giriş yapılmalıdır.");

    /// <summary>A Platform Administrator has no company, so is refused like any non-admin.</summary>
    private void EnsureTenantAdmin()
    {
        RequireSignedIn();
        if (!currentUser.IsTenantAdmin || currentUser.TenantId is null)
        {
            throw new ForbiddenException(TenantSettingsService.TenantAdminRequired);
        }
    }
}
