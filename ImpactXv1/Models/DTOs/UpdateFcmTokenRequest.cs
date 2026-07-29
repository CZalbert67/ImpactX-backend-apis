using System.ComponentModel.DataAnnotations;

namespace ImpactX.Models.DTOs;

public class UpdateFcmTokenRequest
{
    [Required(ErrorMessage = "El token FCM es obligatorio.")]
    [MaxLength(1000, ErrorMessage = "El token FCM no puede exceder los 1000 caracteres.")]
    public string Token { get; set; } = string.Empty;
}
