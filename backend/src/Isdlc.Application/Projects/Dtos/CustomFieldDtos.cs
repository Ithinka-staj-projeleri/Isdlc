using System.ComponentModel.DataAnnotations;
using Isdlc.Domain.Projects;

namespace Isdlc.Application.Projects.Dtos;

/// <summary>A company field of the request form (Faz 7 · A6). Contract: docs/configurability.md → Şirkete özel alanlar.</summary>
public record CustomFieldDto(
    int Id,
    string Key,
    string Label,
    CustomFieldType Type,
    IReadOnlyList<string> Options, // only for Select
    bool IsRequired,
    string? HelpText,
    int Order,
    bool IsActive);

/// <summary>
/// The fields both POST and PUT /custom-fields send. Key and Type are only given when the field is added; they never change.
/// </summary>
public abstract record CustomFieldFormDto : IValidatableObject
{
    [Required(ErrorMessage = "Alan adı zorunludur.")]
    [MaxLength(CustomField.MaxLabelLength, ErrorMessage = "Alan adı en fazla 100 karakter olabilir.")]
    public string? Label { get; init; }

    /// <summary>The choices of a Select field: 1–50, each 1–100 characters, no repeats. Leave empty for other types.</summary>
    public List<string>? Options { get; init; }

    public bool IsRequired { get; init; }

    [MaxLength(CustomField.MaxHelpTextLength, ErrorMessage = "Açıklama en fazla 300 karakter olabilir.")]
    public string? HelpText { get; init; }

    /// <summary>The field's place on the form. Empty: after the last field.</summary>
    [Range(0, 1000, ErrorMessage = "Sıra 0 ile 1000 arasında olmalıdır.")]
    public int? Order { get; init; }

    /// <summary>The field's type: given on POST, read from the saved field on PUT.</summary>
    protected abstract CustomFieldType? FieldType { get; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (FieldType is { } type && OptionsError(type, Options) is { } error)
        {
            yield return new ValidationResult(error, [nameof(Options)]);
        }
    }

    /// <summary>What is wrong with the options for a field of this type; null when nothing is.</summary>
    public static string? OptionsError(CustomFieldType type, IReadOnlyList<string>? options)
    {
        options ??= [];

        if (type != CustomFieldType.Select)
        {
            return options.Count > 0 ? "Seçenekler yalnızca seçim alanında kullanılır." : null;
        }

        if (options.Count == 0)
        {
            return "Seçim alanının en az bir seçeneği olmalıdır.";
        }
        if (options.Count > CustomField.MaxOptions)
        {
            return "En fazla 50 seçenek eklenebilir.";
        }
        if (options.Any(o => string.IsNullOrWhiteSpace(o) || o.Trim().Length > CustomField.MaxOptionLength))
        {
            return "Her seçenek 1 ile 100 karakter arasında olmalıdır.";
        }
        if (options.Select(o => o.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != options.Count)
        {
            return "Aynı seçenek iki kez yazılamaz.";
        }

        return null;
    }
}

/// <summary>POST /custom-fields — a new active field.</summary>
public record CreateCustomFieldDto : CustomFieldFormDto
{
    [Required(ErrorMessage = "Alan anahtarı zorunludur.")]
    [StringLength(CustomField.MaxKeyLength, MinimumLength = CustomField.MinKeyLength,
        ErrorMessage = "Alan anahtarı 2 ile 50 karakter arasında olmalıdır.")]
    [RegularExpression(CustomField.KeyPattern,
        ErrorMessage = "Alan anahtarında yalnızca küçük harf, rakam ve tire kullanılabilir.")]
    public string? Key { get; init; }

    [Required(ErrorMessage = "Alan türü zorunludur.")]
    public CustomFieldType? Type { get; init; }

    protected override CustomFieldType? FieldType => Type;
}

/// <summary>
/// PUT /custom-fields/{id} — everything but Key and Type. IsActive false hides the field from the form; its values stay.
/// Options are checked against the saved field's type by the service (OptionsError).
/// </summary>
public record UpdateCustomFieldDto : CustomFieldFormDto
{
    [Required(ErrorMessage = "Alanın aktif olup olmadığı belirtilmelidir.")]
    public bool? IsActive { get; init; }

    protected override CustomFieldType? FieldType => null;
}
