using System.Security.Cryptography;
using Isdlc.Application.Identity;

namespace Isdlc.Infrastructure.Identity;

/// <summary>PBKDF2-SHA256 with a random salt per password. Format: v1.{iterations}.{salt}.{key} (base64).</summary>
public class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 210_000;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return $"v1.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
    }

    public bool Verify(string hash, string password)
    {
        var parts = hash.Split('.');
        // 0 or a negative count would make Pbkdf2 throw, turning a broken row into a 500 instead of a failed sign-in.
        if (parts.Length != 4 || parts[0] != "v1" || !int.TryParse(parts[1], out var iterations) || iterations <= 0)
        {
            return false;
        }

        try
        {
            var salt = FromBase64Safe(parts[2]);
            var expected = FromBase64Safe(parts[3]);
            // The key must have the length Hash writes: an empty key would compare equal to an empty result
            // and accept any password.
            if (salt == null || expected == null || expected.Length != KeySize) return false;

            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static byte[]? FromBase64Safe(string base64)
    {
        try { return Convert.FromBase64String(base64); }
        catch { return null; }
    }
}