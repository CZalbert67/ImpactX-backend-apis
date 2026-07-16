namespace Prueba1.Core.Domain;

public class Ruta
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UsuarioId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Origen { get; set; } = string.Empty;
    public double OrigenLat { get; set; }
    public double OrigenLng { get; set; }
    public string Destino { get; set; } = string.Empty;
    public double DestinoLat { get; set; }
    public double DestinoLng { get; set; }
    public double DistanciaKm { get; set; }
    public int DuracionEstimadaMin { get; set; }
    public bool EsFrecuente { get; set; }
    public bool SeleccionadaHoy { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public DateTime? UsadaEn { get; set; }
}
