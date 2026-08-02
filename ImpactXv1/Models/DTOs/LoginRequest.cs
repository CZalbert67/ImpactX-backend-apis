using System.ComponentModel.DataAnnotations;

namespace ImpactX.Models.DTOs;

public class LoginRequest
{
    // Legacy: correo electrónico. Se conserva para clientes que aún no envían
    // el nuevo contrato con `identifier`. Al menos uno debe estar presente.
    [MaxLength(256)]
    public string? Correo { get; set; }

    // Identificador de login (email o username), contrato V1.
    [MaxLength(256)]
    public string? Identifier { get; set; }

    [Required]
    public string Password { get; set; } = string.Empty;

    [RegularExpression("^(web|mobile|wearable)$", ErrorMessage = "El cliente debe ser web, mobile o wearable.")]
    public string Client { get; set; } = "mobile";
}
