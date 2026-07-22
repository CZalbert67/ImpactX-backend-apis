using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace ImpactX.Core.Domain;

public class RefreshToken
{
    [JsonProperty("id")]
    [JsonPropertyName("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [JsonProperty("usuarioId")]
    [JsonPropertyName("usuarioId")]
    public Guid UsuarioId { get; set; }

    [JsonProperty("token")]
    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;

    [JsonProperty("deviceInfo")]
    [JsonPropertyName("deviceInfo")]
    public string? DeviceInfo { get; set; }

    [JsonProperty("expiresAt")]
    [JsonPropertyName("expiresAt")]
    public DateTime ExpiresAt { get; set; }

    [JsonProperty("createdAt")]
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonProperty("revokedAt")]
    [JsonPropertyName("revokedAt")]
    public DateTime? RevokedAt { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    public bool IsActive => RevokedAt is null && !IsExpired;
}
