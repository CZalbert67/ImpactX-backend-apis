using System.ComponentModel.DataAnnotations;
using ImpactX.Core.Domain;
using ImpactX.Models.DTOs.FamilySubscriptions;
using ImpactX.Models.DTOs.Monitoring;
using ImpactX.Models.DTOs.QuickMessages;
using ImpactX.Models.DTOs.Vehicles;

namespace ImpactX.Models.DTOs;

public sealed class AccountExportV2Dto
{
    public int ContractVersion { get; set; } = 2;
    public DateTime GeneratedAtUtc { get; set; }
    public UserProfileDto Profile { get; set; } = new();
    public EffectiveSubscriptionDto EffectiveSubscription { get; set; } = new();
    public IReadOnlyList<SuscripcionDto> SubscriptionHistory { get; set; } = [];
    public IReadOnlyList<PagoDto> Payments { get; set; } = [];
    public FamilySubscriptionSummaryDto? FamilySubscription { get; set; }
    public WearableDto? Wearable { get; set; }
    public IReadOnlyList<VehicleDto> Vehicles { get; set; } = [];
    public EmergencyContactSyncResponse EmergencyContacts { get; set; } = new();
    public IReadOnlyList<MonitoringRelationshipDto> MonitoringRelationships { get; set; } = [];
    public IReadOnlyList<Viaje> Trips { get; set; } = [];
    public IReadOnlyList<ViajeTelemetry> Telemetry { get; set; } = [];
    public IReadOnlyList<Alerta> Alerts { get; set; } = [];
    public IReadOnlyList<Incidente> Incidents { get; set; } = [];
    public IReadOnlyList<NotificacionDto> Notifications { get; set; } = [];
    public IReadOnlyList<QuickMessageDto> QuickMessages { get; set; } = [];
}

public sealed class AccountRetentionDto
{
    public int TripsAndTelemetryDays { get; set; } = 90;
    public int AlertsAndIncidentsDays { get; set; } = 365;
    public int NotificationsDays { get; set; } = 30;
    public bool AccountActive { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public DateTime? DataAnonymizedAtUtc { get; set; }
    public string DeletionMode { get; set; } = "immediate-identity-anonymization;domain-records-follow-ttl";
}

public sealed class RevokeConsentsRequest
{
    public bool RevokeLocationIncidentConsent { get; set; }
    public bool RevokeDrivingPatternConsent { get; set; }
    public bool RemoveMedicalProfile { get; set; }
}

public sealed class DeleteAccountV2Request
{
    [Required]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string Confirmation { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Reason { get; set; }
}

public sealed class DeleteAccountV2Response
{
    public bool Deleted { get; set; }
    public DateTime DeletedAtUtc { get; set; }
    public bool IdentityAnonymized { get; set; }
    public string RetentionSummary { get; set; } = string.Empty;
}
