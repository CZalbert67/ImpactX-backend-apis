using System.Text.Json.Serialization;
using ImpactX.Core.Domain.Enums;

namespace ImpactX.Core.Domain;

public class Vehicle
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Public identifier exposed via API. Never expose <see cref="Id"/>.</summary>
    [JsonPropertyName("publicVehicleId")]
    public string PublicVehicleId { get; set; } = string.Empty;

    /// <summary>Internal FK to the owning user. Partition key in Cosmos.</summary>
    [JsonPropertyName("ownerUserId")]
    public Guid OwnerUserId { get; set; }

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

    [JsonPropertyName("activo")]
    public bool Activo { get; set; } = true;

    [JsonPropertyName("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("updatedAtUtc")]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("deletedAtUtc")]
    public DateTime? DeletedAtUtc { get; set; }
}
