using Isdlc.Domain.Identity;

namespace Isdlc.Application.Identity;

public record TokenResult(string Value, DateTime ExpiresAt);

public interface ITokenService
{
    TokenResult Create(User user);
}