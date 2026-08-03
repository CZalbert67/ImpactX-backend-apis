using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace ImpactX.Core.Domain;

public sealed class MobileSyncOperationReceipt
{
    [JsonProperty("operationId")]
    [JsonPropertyName("operationId")]
    public Guid OperationId { get; set; }

    [JsonProperty("type")]
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("result")]
    [JsonPropertyName("result")]
    public string Result { get; set; } = "applied";

    [JsonProperty("message")]
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonProperty("appliedAtUtc")]
    [JsonPropertyName("appliedAtUtc")]
    public DateTime AppliedAtUtc { get; set; } = DateTime.UtcNow;
}
