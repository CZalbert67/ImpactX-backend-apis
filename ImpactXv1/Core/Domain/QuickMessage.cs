using System.Text.Json.Serialization;

namespace ImpactX.Core.Domain;

public class QuickMessage
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [JsonPropertyName("publicMessageId")]
    public string PublicMessageId { get; set; } = string.Empty;

    [JsonPropertyName("senderUserId")]
    public Guid SenderUserId { get; set; }

    [JsonPropertyName("recipientUserId")]
    public Guid RecipientUserId { get; set; }

    [JsonPropertyName("publicRelationshipId")]
    public string PublicRelationshipId { get; set; } = string.Empty;

    [JsonPropertyName("publicTemplateId")]
    public string PublicTemplateId { get; set; } = string.Empty;

    [JsonPropertyName("textSnapshot")]
    public string TextSnapshot { get; set; } = string.Empty;

    [JsonPropertyName("routePublicId")]
    public string? RoutePublicId { get; set; }

    [JsonPropertyName("incidentPublicId")]
    public string? IncidentPublicId { get; set; }

    [JsonPropertyName("sentAtUtc")]
    public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("isRead")]
    public bool IsRead { get; set; }

    [JsonPropertyName("readAtUtc")]
    public DateTime? ReadAtUtc { get; set; }
}
