using System;
using System.Text.Json.Serialization;
using ImpactX.Core.Domain.Enums;

namespace ImpactX.Models.DTOs.Vehicles;

public class VehicleDto
{
    [JsonPropertyName("publicVehicleId")]
    public string PublicVehicleId { get; set; } = string.Empty;

    [JsonPropertyName("tipoVehiculo")]
    public TipoVehiculo TipoVehiculo { get; set; }

    [JsonPropertyName("marca")]
    public string Marca { get; set; } = string.Empty;

    [JsonPropertyName("modelo")]
    public string Modelo { get; set; } = string.Empty;

    [JsonPropertyName("ano")]
    public int Ano { get; set; }

    [JsonPropertyName("velocidadPromedio")]
    public double VelocidadPromedio { get; set; }

    [JsonPropertyName("usoPrincipalVehiculo")]
    public UsoPrincipalVehiculo UsoPrincipalVehiculo { get; set; }

    [JsonPropertyName("esPrincipal")]
    public bool EsPrincipal { get; set; }

    [JsonPropertyName("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; }

    [JsonPropertyName("updatedAtUtc")]
    public DateTime UpdatedAtUtc { get; set; }
}
