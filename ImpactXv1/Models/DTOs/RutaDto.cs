namespace ImpactX.Models.DTOs;

public class RutaDto
{
    public Guid Id { get; set; }
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
    public DateTime CreadoEn { get; set; }
    public DateTime? UsadaEn { get; set; }
}

public class CreateRutaRequest
{
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
}

public class UpdateRutaRequest
{
    public string? Nombre { get; set; }
    public string? Origen { get; set; }
    public double? OrigenLat { get; set; }
    public double? OrigenLng { get; set; }
    public string? Destino { get; set; }
    public double? DestinoLat { get; set; }
    public double? DestinoLng { get; set; }
    public double? DistanciaKm { get; set; }
    public int? DuracionEstimadaMin { get; set; }
    public bool? EsFrecuente { get; set; }
}

public class SelectTodayRequest
{
    public Guid RutaId { get; set; }
}
