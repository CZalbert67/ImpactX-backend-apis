using System.ComponentModel.DataAnnotations;

namespace ImpactX.Models.DTOs;

public class RecoverPasswordRequest
{
    [Required, MaxLength(256), EmailAddress]
    public string Correo { get; set; } = string.Empty;
}
