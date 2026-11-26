using Isdlc.Domain.Common;

namespace Isdlc.Domain.Projects;

/// <summary>
/// A field the company adds to its request form (Faz 7 · A6). The definition is company data; the value is kept on the
/// record (Project.CustomValues) under Key. Never deleted: an inactive field is hidden from the form, its values stay.
/// Rules: docs/configurability.md → Şirkete özel alanlar.
/// </summary>
public class CustomField : AuditableEntity, ITenantOwned
{
    public const int MinKeyLength = 2;
    public const int MaxKeyLength = 50;
    public const int MaxLabelLength = 100;
    public const int MaxHelpTextLength = 300;
    public const int MaxOptions = 50;
    public const int MaxOptionLength = 100;

    /// <summary>The longest text value a record can keep in a Text field.</summary>
    public const int MaxTextValueLength = 500;

    /// <summary>Small letters, digits and dashes: "butce-onayli".</summary>
    public const string KeyPattern = "^[a-z0-9-]+$";

    public int Id { get; set; }

    public int TenantId { get; set; }

    /// <summary>The name in Project.CustomValues and in skip rules ("custom:{key}"). Unique in the company; never changes.</summary>
    public string Key { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    /// <summary>Never changes: the values already saved and the rules that look at the field depend on it.</summary>
    public CustomFieldType Type { get; set; }

    /// <summary>The choices of a Select field; empty for every other type.</summary>
    public List<string> Options { get; set; } = new();

    /// <summary>Required on the request form and in the "Talep Kayıt Formu dolu" prerequisite.</summary>
    public bool IsRequired { get; set; }

    public string? HelpText { get; set; }

    /// <summary>The field's place on the form.</summary>
    public int Order { get; set; }

    public bool IsActive { get; set; } = true;
}

public enum CustomFieldType
{
    Text,
    Number,
    Date,
    Select,
    Boolean,
}
