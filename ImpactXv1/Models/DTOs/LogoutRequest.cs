using System.ComponentModel.DataAnnotations;

namespace Prueba1.Models.DTOs;

public class LogoutRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}
