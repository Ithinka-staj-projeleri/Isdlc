using Isdlc.Domain.Identity;

namespace Isdlc.Application.Identity.Dtos;

public record AuthUserDto(int Id, string FullName, string Email, UserRole Role);
public record LoginResultDto(string Token, DateTime ExpiresAt, AuthUserDto User);