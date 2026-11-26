using Isdlc.Application.Identity.Dtos;
using Isdlc.Domain.Identity;

namespace Isdlc.Application.Identity;

/// <summary>
/// Users of the Identity module. Written by Dide for the project form (docs/projects-api.md, decision 5);
/// Baran adds sign-in and user management next to it.
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Active users only, sorted by name. With a permission, only those who have it (the analyst selector asks for
    /// Analysis, the project manager selector for ProjectManagement); without one, all active users.
    /// </summary>
    Task<IReadOnlyList<UserLookupDto>> ListActiveAsync(Permission? permission, CancellationToken cancellationToken);
}
