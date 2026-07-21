namespace ImpactX.Models.DTOs;

public class ViajeDto
{
    public Guid Id { get; set; }
    public string DispositivoId { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public DateTime Inicio { get; set; }
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

public class StartTripRequest
{
    public string DispositivoId { get; set; } = string.Empty;
    public string? Proposito { get; set; }
    public string? RutaOrigen { get; set; }
    public string? RutaDestino { get; set; }
}

public class TelemetryUpdateRequest
{
    public List<TelemetryPointDto> Puntos { get; set; } = [];
}

public class TripActionResponse
{
    public Guid ViajeId { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
}
