namespace ImpactX.Core.Domain;

public class PreferenciasUsuario
{
    public bool NotificacionesPush { get; set; } = true;
    public bool NotificacionesEmail { get; set; } = true;
    public bool CompartirUbicacion { get; set; } = true;
    public string? Idioma { get; set; } = "es";
    public string? UnidadVelocidad { get; set; } = "kmh";
}
