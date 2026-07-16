namespace Prueba1.Core.Domain;

public class PermisosApp
{
    public PermisosPlataforma? Mobile { get; set; }
    public PermisosPlataforma? Web { get; set; }
}

public class PermisosPlataforma
{
    public bool Ubicacion { get; set; }
    public bool Notificaciones { get; set; }
    public bool Camara { get; set; }
    public bool Microfono { get; set; }
    public bool Sensores { get; set; }
    public bool Bluetooth { get; set; }
}
