namespace Isdlc.Application.Identity;

/// <summary>
/// The API keeps whether a user may go on for a minute (docs/multi-tenancy.md → İstek başına). When an administrator
/// deactivates a user, that answer is forgotten at once, so the user is signed out on their next request.
/// </summary>
public interface IAccountStatusCache
{
    void Forget(int userId, int? tenantId);
}
