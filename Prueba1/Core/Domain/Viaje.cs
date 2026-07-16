namespace Prueba1.Core.Domain;

public class Viaje
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UsuarioId { get; set; }
    public string DispositivoId { get; set; } = string.Empty;
    public string Estado { get; set; } = "Activo";
    public DateTime Inicio { get; set; } = DateTime.UtcNow;
    public DateTime? Fin { get; set; }
    public double? DistanciaRecorridaKm { get; set; }
    public int? DuracionMinutos { get; set; }
    public double? VelocidadPromedio { get; set; }
    public double? VelocidadMaxima { get; set; }
    public string? RiesgoMaximo { get; set; }
    public string? Proposito { get; set; }
    public string? RutaOrigen { get; set; }
    public string? RutaDestino { get; set; }
}
