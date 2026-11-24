using Isdlc.Application.Identity.Dtos;

namespace Isdlc.Application.Identity;

public interface IAuthService
{
    /// <summary>Null when the sign-in fails, whatever the reason.</summary>
    Task<LoginResultDto?> LoginAsync(LoginDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes the signed-in user's password and clears MustChangePassword; returns a new token without the
    /// "mustChangePassword" claim. 400 when the current password is wrong or the new one is the same.
    /// </summary>
    Task<LoginResultDto> ChangePasswordAsync(int userId, ChangePasswordDto request, CancellationToken cancellationToken = default);
}
