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

    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;

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
