using System.ComponentModel.DataAnnotations;
using Isdlc.Application.Identity.Dtos;

namespace Isdlc.Application.Tests.Identity;

/// <summary>The login request body's rules (shown under the form fields as Turkish 400 errors); no database needed.</summary>
public class LoginDtoTests
{
    private static List<ValidationResult> Validate(string email, string password)
    {
        var dto = new LoginDto { Email = email, Password = password };
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void Validate_EmailAndPassword_IsValid()
    {
        Assert.Empty(Validate("elif.yilmaz@isdlc.local", "Gizli-Parola-1"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MissingEmail_ReturnsTurkishErrorOnEmail(string email)
    {
        var error = Assert.Single(Validate(email, "Gizli-Parola-1"));

        Assert.Equal([nameof(LoginDto.Email)], error.MemberNames);
        Assert.Equal("E-posta zorunludur.", error.ErrorMessage);
    }

    [Fact]
    public void Validate_MissingPassword_ReturnsTurkishErrorOnPassword()
    {
        var error = Assert.Single(Validate("elif.yilmaz@isdlc.local", ""));

        Assert.Equal([nameof(LoginDto.Password)], error.MemberNames);
        Assert.Equal("Parola zorunludur.", error.ErrorMessage);
    }

    [Fact]
    public void Validate_TooLongValues_ReturnsErrorOnEachField()
    {
        var results = Validate(new string('a', 248) + "@isdlc.lo", new string('p', 201)); // 257 and 201 characters

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(LoginDto.Email)));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(LoginDto.Password)));
        Assert.All(results, r => Assert.StartsWith("En fazla", r.ErrorMessage));
    }

    [Fact]
    public void Validate_LongestAllowedValues_IsValid()
    {
        Assert.Empty(Validate(new string('a', 247) + "@isdlc.lo", new string('p', 200))); // 256 and 200 characters
    }
}
