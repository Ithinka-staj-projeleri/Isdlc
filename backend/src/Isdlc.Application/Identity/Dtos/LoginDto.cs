using System.ComponentModel.DataAnnotations;

namespace Isdlc.Application.Identity.Dtos;

public class LoginDto
{
    [Required(ErrorMessage = "E-posta zorunludur.")]
    [MaxLength(256, ErrorMessage = "En fazla 256 karakter olabilir.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Parola zorunludur.")]
    [MaxLength(200, ErrorMessage = "En fazla 200 karakter olabilir.")]
    public string Password { get; set; } = string.Empty;
}