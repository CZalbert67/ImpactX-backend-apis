using System.ComponentModel.DataAnnotations;

namespace Prueba1.Models.DTOs;

public class LoginRequest
{
    [Required, MaxLength(256), EmailAddress]
    public string Correo { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}