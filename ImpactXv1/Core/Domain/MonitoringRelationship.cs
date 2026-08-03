using System.Text.Json.Serialization;
using ImpactX.Core.Domain.Enums;

namespace ImpactX.Core.Domain;

public class MonitoringRelationship
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [JsonPropertyName("publicRelationshipId")]
    public string PublicRelationshipId { get; set; } = string.Empty;

    [JsonPropertyName("monitorUserId")]
    public Guid MonitorUserId { get; set; }

    [JsonPropertyName("monitoredUserId")]
    public Guid? MonitoredUserId { get; set; }

    [JsonPropertyName("initiatedByUserId")]
    public Guid InitiatedByUserId { get; set; }

    [JsonPropertyName("direction")]
    public MonitoringRequestDirection Direction { get; set; }

    [JsonPropertyName("status")]
    public MonitoringRelationshipStatus Status { get; set; } = MonitoringRelationshipStatus.Pending;

    [JsonPropertyName("targetEmailNormalized")]
    public string? TargetEmailNormalized { get; set; }

    [JsonPropertyName("targetPublicProfileId")]
    public string? TargetPublicProfileId { get; set; }

    [JsonPropertyName("targetUsername")]
    public string? TargetUsername { get; set; }

    [JsonPropertyName("invitationCodeHash")]
    public string InvitationCodeHash { get; set; } = string.Empty;

    [JsonPropertyName("permissions")]
    public MonitoringPermissions Permissions { get; set; } = new();

    [JsonPropertyName("requestedAtUtc")]
    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("expiresAtUtc")]
    public DateTime ExpiresAtUtc { get; set; }

    [JsonPropertyName("acceptedAtUtc")]
    public DateTime? AcceptedAtUtc { get; set; }

    [JsonPropertyName("revokedAtUtc")]
    public DateTime? RevokedAtUtc { get; set; }

    [JsonPropertyName("medicalConsentGrantedAtUtc")]
    public DateTime? MedicalConsentGrantedAtUtc { get; set; }

    [JsonPropertyName("updatedAtUtc")]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [Newtonsoft.Json.JsonProperty("_etag")]
    [JsonPropertyName("_etag")]
    public string? ETag { get; set; }
}

public class MonitoringPermissions
{
    [JsonPropertyName("viewRoutes")]
    public bool ViewRoutes { get; set; } = true;

    [JsonPropertyName("viewLocation")]
    public bool ViewLocation { get; set; } = true;

    [JsonPropertyName("viewEmergencyLocation")]
    public bool ViewEmergencyLocation { get; set; } = true;

    [JsonPropertyName("viewIncidents")]
    public bool ViewIncidents { get; set; } = true;

    [JsonPropertyName("receiveCriticalAlerts")]
    public bool ReceiveCriticalAlerts { get; set; } = true;

    [JsonPropertyName("viewMedicalProfile")]
    public bool ViewMedicalProfile { get; set; }

    [JsonPropertyName("sendMessages")]
    public bool SendMessages { get; set; } = true;

    [JsonPropertyName("viewTelemetry")]
    public bool ViewTelemetry { get; set; } = true;

    [JsonPropertyName("receiveNotifications")]
    public bool ReceiveNotifications { get; set; } = true;
}
