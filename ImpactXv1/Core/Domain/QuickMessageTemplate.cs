using System.Text.Json.Serialization;

namespace ImpactX.Core.Domain;

public class QuickMessageTemplate
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [JsonPropertyName("publicTemplateId")]
    public string PublicTemplateId { get; set; } = string.Empty;

    [JsonPropertyName("ownerUserId")]
    public Guid OwnerUserId { get; set; }

    [JsonPropertyName("ownerKey")]
    public string OwnerKey { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("sortOrder")]
    public int SortOrder { get; set; }

    [JsonPropertyName("active")]
    public bool Active { get; set; } = true;

    [JsonPropertyName("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("updatedAtUtc")]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("deletedAtUtc")]
    public DateTime? DeletedAtUtc { get; set; }
}
