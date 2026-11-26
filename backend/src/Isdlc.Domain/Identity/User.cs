namespace Isdlc.Domain.Identity;

/// <summary>
/// A person who signs in. Users are never deleted, only deactivated (IsActive = false).
/// </summary>
public class User
{
    public int Id { get; set; }

    /// <summary>The company. Empty only for the Platform Administrator (Faz 6 · T4); not ITenantOwned for that reason.</summary>
    public int? TenantId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    /// <summary>Set by the Identity module. Empty until then.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Platform Yöneticisi: opens and deactivates companies. Belongs to no company (TenantId empty), sees no company's records
    /// and has no permissions. Only from configuration (docs/multi-tenancy.md, docs/configurability.md).
    /// </summary>
    public bool IsPlatformAdmin { get; set; }

    /// <summary>What the user can do in the flow (Faz 7 · A2). Empty: work outside the flow only.</summary>
    public ICollection<UserPermission> Permissions { get; set; } = new List<UserPermission>();

    /// <summary>
    /// Şirket Yöneticisi (Faz 6 · T4): manages the users of their own company. A mark, not a permission, so anyone can also
    /// be one. Always false for the Platform Administrator.
    /// </summary>
    public bool IsTenantAdmin { get; set; }

    /// <summary>Opened with a temporary password: every request but changing it gets 403 until it is changed.</summary>
    public bool MustChangePassword { get; set; }

    /// <summary>Areas of expertise, comma separated plain text (Faz 5 · F9). Null when not filled.</summary>
    public string? Expertise { get; set; }

    /// <summary>The person's "how to work with me" note (Faz 5 · F9). Null when not filled.</summary>
    public string? WorkingNote { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>Lengths of the profile fields, shared by the database configuration and the form's rules (docs/users-api.md, P1).</summary>
public static class UserProfileLimits
{
    public const int Expertise = 300;
    public const int WorkingNote = 1000;
}

/// <summary>Password rules of temporary and new passwords (docs/multi-tenancy.md → Parola).</summary>
public static class PasswordRules
{
    public const int MinLength = 12;
    public const int MaxLength = 200;
}
