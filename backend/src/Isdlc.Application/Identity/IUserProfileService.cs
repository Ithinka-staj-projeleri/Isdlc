using Isdlc.Application.Identity.Dtos;

namespace Isdlc.Application.Identity;

/// <summary>
/// Kişi profili and the owner suggestion (Faz 5 · F9). The numbers come from the team workload (ITeamWorkloadService).
/// Contract: docs/users-api.md
/// </summary>
public interface IUserProfileService
{
    /// <summary>Anyone's profile, inactive users too (P3). NotFoundException when there is no such user.</summary>
    Task<UserProfileDto> GetAsync(int userId, CancellationToken cancellationToken);

    /// <summary>Saves the signed-in user's own expertise and note (P2) and returns their profile.</summary>
    Task<UserProfileDto> UpdateMineAsync(UpdateMyProfileDto dto, CancellationToken cancellationToken);

    /// <summary>Active users with their expertise and load, sorted by name (P7).</summary>
    Task<IReadOnlyList<UserWorkloadDto>> ListWorkloadAsync(CancellationToken cancellationToken);
}
