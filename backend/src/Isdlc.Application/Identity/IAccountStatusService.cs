namespace Isdlc.Application.Identity;

/// <summary>
/// Whether a signed-in user may go on: the user is active, still of the company in their token, and the company is
/// active. Checked on every request (the API caches the answer for a minute), so a deactivated user or company is
/// signed out without waiting for the token to expire (docs/multi-tenancy.md → İstek başına).
/// </summary>
public interface IAccountStatusService
{
    Task<bool> IsActiveAsync(int userId, int? tenantId, CancellationToken cancellationToken);
}
