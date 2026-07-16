using System.ComponentModel.DataAnnotations;

namespace Prueba1.Models.DTOs;

public class RecoverPasswordRequest
{
    [Required, MaxLength(256), EmailAddress]
    public string Correo { get; set; } = string.Empty;
}
