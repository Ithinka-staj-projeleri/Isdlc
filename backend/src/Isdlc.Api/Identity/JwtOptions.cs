namespace Isdlc.Api.Identity;

public class JwtOptions
{
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    /// <summary>At least 32 characters. Never in the repository: dotnet user-secrets or the Jwt__SigningKey variable.</summary>
    public string SigningKey { get; set; } = string.Empty;
    public int ExpiresInMinutes { get; set; } = 480;
}