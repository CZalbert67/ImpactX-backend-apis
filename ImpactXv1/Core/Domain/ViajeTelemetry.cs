namespace ImpactX.Core.Domain;

public class ViajeTelemetry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ViajeId { get; set; }
    public Guid UsuarioId { get; set; }
    public DateTime Timestamp { get; set; }
    public double Lat { get; set; }
    public double Lng { get; set; }
    public double Velocidad { get; set; }
    public double? Altitud { get; set; }
    public double? Heading { get; set; }
}
