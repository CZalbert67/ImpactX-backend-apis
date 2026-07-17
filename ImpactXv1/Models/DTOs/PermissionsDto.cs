namespace ImpactX.Models.DTOs;

public class UpdatePermissionsRequest
{
    public bool Ubicacion { get; set; }
    public bool Notificaciones { get; set; }
    public bool Camara { get; set; }
    public bool Microfono { get; set; }
    public bool Sensores { get; set; }
    public bool Bluetooth { get; set; }
}
