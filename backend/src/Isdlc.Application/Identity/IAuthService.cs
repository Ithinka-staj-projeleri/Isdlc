using Isdlc.Application.Identity.Dtos;

namespace Isdlc.Application.Identity;

public interface IAuthService
{
    /// <summary>Null when the sign-in fails, whatever the reason.</summary>
    Task<LoginResultDto?> LoginAsync(LoginDto request, CancellationToken cancellationToken = default);
}