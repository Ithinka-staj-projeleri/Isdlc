using Isdlc.Application.Identity.Dtos;

namespace Isdlc.Application.Identity;

/// <summary>
/// Şirket Yöneticisi (Faz 6 · T4): the users of the administrator's own company. Every call is 403 for anyone else
/// ("Bu işlem için şirket yöneticisi olmalısınız."). Contract: docs/multi-tenancy.md → Uç noktalar, Hata mesajları.
/// </summary>
public interface IUserAdminService
{
    /// <summary>Every user of the company, inactive ones too, sorted by name.</summary>
    Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken cancellationToken);

    /// <summary>A new user with a temporary password (MustChangePassword). 409 when the e-mail is used in any company.</summary>
    Task<UserDto> CreateAsync(CreateUserDto request, CancellationToken cancellationToken);

    /// <summary>
    /// Name, role, active and administrator mark. 400 when deactivating oneself or leaving the company without an
    /// active administrator; 404 for a user of another company.
    /// </summary>
    Task<UserDto> UpdateAsync(int userId, UpdateUserDto request, CancellationToken cancellationToken);

    /// <summary>Gives a temporary password; the user changes it at the next sign-in.</summary>
    Task ResetPasswordAsync(int userId, ResetPasswordDto request, CancellationToken cancellationToken);
}
