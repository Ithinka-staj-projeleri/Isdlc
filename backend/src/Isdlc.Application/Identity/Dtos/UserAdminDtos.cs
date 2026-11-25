using System.ComponentModel.DataAnnotations;
using Isdlc.Domain.Identity;

namespace Isdlc.Application.Identity.Dtos;

// Contract: docs/multi-tenancy.md → Uç noktalar, Şirket Yöneticisi.

/// <summary>A user of the company as the user list shows them (GET /users).</summary>
public record UserDto(
    int Id, string FullName, string Email, UserRole Role, bool IsActive, bool IsTenantAdmin, bool MustChangePassword);

/// <summary>POST /users: a new user of the signed-in administrator's company, with a temporary password.</summary>
public class CreateUserDto
{
    [Required(ErrorMessage = "Ad soyad zorunludur.")]
    [MaxLength(100, ErrorMessage = "En fazla 100 karakter olabilir.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "E-posta zorunludur.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin.")]
    [MaxLength(256, ErrorMessage = "En fazla 256 karakter olabilir.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Rol seçilmelidir.")]
    public UserRole? Role { get; set; }

    public bool IsTenantAdmin { get; set; }

    [Required(ErrorMessage = "Geçici parola zorunludur.")]
    [MinLength(PasswordRules.MinLength, ErrorMessage = "Parola en az 12 karakter olmalıdır.")]
    [MaxLength(PasswordRules.MaxLength, ErrorMessage = "En fazla 200 karakter olabilir.")]
    public string TemporaryPassword { get; set; } = string.Empty;
}

/// <summary>PUT /users/{id}: everything but the e-mail, which is the sign-in name and never changes.</summary>
public class UpdateUserDto
{
    [Required(ErrorMessage = "Ad soyad zorunludur.")]
    [MaxLength(100, ErrorMessage = "En fazla 100 karakter olabilir.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Rol seçilmelidir.")]
    public UserRole? Role { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsTenantAdmin { get; set; }
}

/// <summary>POST /users/{id}/reset-password.</summary>
public class ResetPasswordDto
{
    [Required(ErrorMessage = "Geçici parola zorunludur.")]
    [MinLength(PasswordRules.MinLength, ErrorMessage = "Parola en az 12 karakter olmalıdır.")]
    [MaxLength(PasswordRules.MaxLength, ErrorMessage = "En fazla 200 karakter olabilir.")]
    public string TemporaryPassword { get; set; } = string.Empty;
}
