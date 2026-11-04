namespace Isdlc.Domain.Identity;

/// <summary>
/// A person who signs in. Users are never deleted, only deactivated (IsActive = false).
/// </summary>
public class User
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    /// <summary>Set by the Identity module. Empty until then.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}
