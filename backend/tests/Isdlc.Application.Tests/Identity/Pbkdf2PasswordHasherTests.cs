using System.Security.Cryptography;
using System.Text;
using Isdlc.Infrastructure.Identity;

namespace Isdlc.Application.Tests.Identity;

/// <summary>The password hasher on its own; no database needed. Format: v1.{iterations}.{salt}.{key} (base64).</summary>
public class Pbkdf2PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    private static string B64(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text));

    // ---- Hash ----

    [Fact]
    public void Hash_Password_UsesV1FormatAndDoesNotContainThePassword()
    {
        var hash = _hasher.Hash("Gizli-Parola-1");

        var parts = hash.Split('.');
        Assert.Equal(4, parts.Length);
        Assert.Equal("v1", parts[0]);
        Assert.True(int.Parse(parts[1]) >= 100_000, "Iteration count should stay high.");
        Assert.Equal(16, Convert.FromBase64String(parts[2]).Length);
        Assert.Equal(32, Convert.FromBase64String(parts[3]).Length);
        Assert.DoesNotContain("Gizli-Parola-1", hash);
    }

    [Fact]
    public void Hash_SamePasswordTwice_GivesDifferentHashes()
    {
        // A random salt per password: equal passwords must not be visible as equal hashes.
        Assert.NotEqual(_hasher.Hash("Ayni-Parola"), _hasher.Hash("Ayni-Parola"));
    }

    // ---- Verify ----

    [Theory]
    [InlineData("Gizli-Parola-1")]
    [InlineData("Şifre-İğüçöı 2026")] // Turkish letters and a space
    [InlineData("x")]
    public void Verify_CorrectPassword_ReturnsTrue(string password)
    {
        var hash = _hasher.Hash(password);

        Assert.True(_hasher.Verify(hash, password));
    }

    [Theory]
    [InlineData("gizli-parola-1")] // case matters
    [InlineData("Gizli-Parola-1 ")]
    [InlineData("")]
    [InlineData("Gizli-Parola-2")]
    public void Verify_WrongPassword_ReturnsFalse(string attempt)
    {
        var hash = _hasher.Hash("Gizli-Parola-1");

        Assert.False(_hasher.Verify(hash, attempt));
    }

    [Fact]
    public void Verify_HashWithOtherIterationCount_UsesTheCountInTheHash()
    {
        // A hash made with fewer iterations (e.g. before the count was raised) must still be accepted.
        var salt = RandomNumberGenerator.GetBytes(16);
        var key = Rfc2898DeriveBytes.Pbkdf2("Eski-Parola", salt, 1_000, HashAlgorithmName.SHA256, 32);
        var hash = $"v1.1000.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";

        Assert.True(_hasher.Verify(hash, "Eski-Parola"));
        Assert.False(_hasher.Verify(hash, "Yeni-Parola"));
    }

    [Theory]
    [InlineData("")] // seed user before the test password is set
    [InlineData("v1")]
    [InlineData("v1.210000.c2FsdA==")]
    [InlineData("v2.210000.c2FsdA==.a2V5")]
    [InlineData("v1.abc.c2FsdA==.a2V5")]
    [InlineData("v1.210000.%%%.a2V5")]
    [InlineData("v1.210000.c2FsdA==.%%%")]
    [InlineData("v1.210000.c2FsdA==.a2V5.extra")]
    public void Verify_BrokenHash_ReturnsFalse(string hash)
    {
        Assert.False(_hasher.Verify(hash, "Gizli-Parola-1"));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    public void Verify_ZeroOrNegativeIterations_ReturnsFalse(string iterations)
    {
        // A corrupted row in the Users table must not crash the login request (it would become a 500).
        var hash = $"v1.{iterations}.{B64("salt-salt-salt-1")}.{B64("key-key-key-key-key-key-key-key!")}";

        Assert.False(_hasher.Verify(hash, "Gizli-Parola-1"));
    }

    [Fact]
    public void Verify_EmptyKey_ReturnsFalse()
    {
        var hash = $"v1.210000.{B64("salt-salt-salt-1")}.";

        Assert.False(_hasher.Verify(hash, "Gizli-Parola-1"));
    }
}
