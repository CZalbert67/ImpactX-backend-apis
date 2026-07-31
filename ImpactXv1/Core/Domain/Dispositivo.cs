using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace ImpactX.Core.Domain;

public class Dispositivo
{
    [JsonProperty("id")]
    [JsonPropertyName("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [JsonProperty("usuarioId")]
    [JsonPropertyName("usuarioId")]
    public Guid UsuarioId { get; set; }

    [JsonProperty("deviceId")]
    [JsonPropertyName("deviceId")]
    public string DeviceId { get; set; } = string.Empty;

    [JsonProperty("platform")]
    [JsonPropertyName("platform")]
    public string Platform { get; set; } = string.Empty;

    [JsonProperty("tokenFcm")]
    [JsonPropertyName("tokenFcm")]
    public string TokenFcm { get; set; } = string.Empty;

    [JsonProperty("nombre")]
    [JsonPropertyName("nombre")]
    public string? Nombre { get; set; }

    [JsonProperty("activo")]
    [JsonPropertyName("activo")]
    public bool Activo { get; set; } = true;

    [JsonProperty("creadoEn")]
    [JsonPropertyName("creadoEn")]
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

    [JsonProperty("actualizadoEn")]
    [JsonPropertyName("actualizadoEn")]
    public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;

    [JsonProperty("ultimoUsoEn")]
    [JsonPropertyName("ultimoUsoEn")]
    public DateTime? UltimoUsoEn { get; set; }
}
