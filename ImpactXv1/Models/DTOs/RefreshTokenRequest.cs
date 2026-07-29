using System.ComponentModel.DataAnnotations;

namespace ImpactX.Models.DTOs;

public class RefreshTokenRequest
{
    [Required(ErrorMessage = "El refresh token es obligatorio.")]
    public string RefreshToken { get; set; } = string.Empty;
}
