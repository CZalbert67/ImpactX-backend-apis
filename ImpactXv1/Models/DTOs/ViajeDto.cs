using System.Text.Json.Serialization;
using ImpactX.Core.Security;

namespace ImpactX.Models.DTOs;

public class ViajeDto
{
    public Guid Id { get; set; }
    public string DispositivoId { get; set; } = string.Empty;
    public string? VehiclePublicId { get; set; }
    public string ControlClient { get; set; } = string.Empty;
    public bool MobileFallbackUsed { get; set; }
    public string? FallbackReason { get; set; }
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
    public string? VehiclePublicId { get; set; }
    public string? FallbackReason { get; set; }

    [JsonIgnore]
    public string Client { get; set; } = ClientTypePolicy.Wearable;
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
