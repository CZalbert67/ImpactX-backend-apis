using System.ComponentModel.DataAnnotations;

namespace ImpactX.Models.DTOs;

public class RegisterRequest
{
    [Required, MaxLength(200)]
    public string Nombre { get; set; } = string.Empty;

    [Required, MaxLength(256), EmailAddress]
    public string Correo { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Telefono { get; set; }

    [Required, MinLength(8), MaxLength(100)]
    public string Password { get; set; } = string.Empty;

    public string? PlanActivo { get; set; }

    [RegularExpression("^(web|mobile|wearable)$", ErrorMessage = "El cliente debe ser web, mobile o wearable.")]
    public string Client { get; set; } = "mobile";
}
