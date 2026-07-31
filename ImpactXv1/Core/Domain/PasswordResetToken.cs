using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace ImpactX.Core.Domain;

public class PasswordResetToken
{
    [JsonProperty("id")]
    [JsonPropertyName("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [JsonProperty("usuarioId")]
    [JsonPropertyName("usuarioId")]
    public Guid UsuarioId { get; set; }

    [JsonProperty("tokenHash")]
    [JsonPropertyName("tokenHash")]
    public string TokenHash { get; set; } = string.Empty;

    [JsonProperty("expiresAt")]
    [JsonPropertyName("expiresAt")]
    public DateTime ExpiresAt { get; set; }

    [JsonProperty("createdAt")]
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonProperty("usedAt")]
    [JsonPropertyName("usedAt")]
    public DateTime? UsedAt { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    public bool IsValid => UsedAt is null && !IsExpired;
}
