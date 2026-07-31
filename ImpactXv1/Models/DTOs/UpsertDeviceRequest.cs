using System.ComponentModel.DataAnnotations;

namespace ImpactX.Models.DTOs;

public class UpsertDeviceRequest
{
    [Required(ErrorMessage = "El DeviceId es obligatorio.")]
    [MaxLength(200, ErrorMessage = "El DeviceId no puede exceder los 200 caracteres.")]
    public string DeviceId { get; set; } = string.Empty;

    [Required(ErrorMessage = "La plataforma es obligatoria.")]
    [MaxLength(20, ErrorMessage = "La plataforma no puede exceder los 20 caracteres.")]
    public string Platform { get; set; } = string.Empty;

    [Required(ErrorMessage = "El token FCM es obligatorio.")]
    [MaxLength(1000, ErrorMessage = "El token FCM no puede exceder los 1000 caracteres.")]
    public string Token { get; set; } = string.Empty;

    [MaxLength(200, ErrorMessage = "El nombre no puede exceder los 200 caracteres.")]
    public string? Name { get; set; }
}
