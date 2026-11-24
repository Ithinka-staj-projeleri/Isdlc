using Isdlc.Domain.Identity;

namespace Isdlc.Application.Identity.Dtos;

/// <summary>The user's company; null for the Platform Administrator.</summary>
public record TenantSummaryDto(int Id, string Code, string Name);

public record AuthUserDto(
    int Id, string FullName, string Email, UserRole Role, TenantSummaryDto? Tenant, bool IsTenantAdmin);

/// <summary>
/// The sign-in (and password change) answer. MustChangePassword: the user signed in with a temporary password and the
/// screen goes to the password change page; every other request gets 403 until it is changed.
/// </summary>
public record LoginResultDto(string Token, DateTime ExpiresAt, AuthUserDto User, bool MustChangePassword);
