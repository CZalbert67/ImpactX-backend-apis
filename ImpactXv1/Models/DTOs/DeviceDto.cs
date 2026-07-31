namespace ImpactX.Models.DTOs;

public class DeviceDto
{
    public Guid Id { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string? Nombre { get; set; }
    public bool Activo { get; set; }
    public DateTime CreadoEn { get; set; }
    public DateTime ActualizadoEn { get; set; }
    public DateTime? UltimoUsoEn { get; set; }
}
