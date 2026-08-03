using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using ImpactX.Models.DTOs.Monitoring;
using ImpactX.Models.DTOs.QuickMessages;
using ImpactX.Models.DTOs.Vehicles;

namespace ImpactX.Models.DTOs;

/// <summary>
/// Snapshot consistente para que la aplicación móvil reconstruya su estado
/// local después de iniciar sesión, reinstalarse o recuperar conectividad.
/// Es de solo lectura y no concede al móvil control sobre viajes/telemetría.
/// </summary>
public sealed class MobileSyncSnapshotDto
{
    public int ContractVersion { get; set; } = 2;
    public Guid SnapshotId { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
    public string SyncCursor { get; set; } = string.Empty;
    public UserProfileDto Profile { get; set; } = new();
    public EffectiveSubscriptionDto EffectiveSubscription { get; set; } = new();
    public PermisosDto Permissions { get; set; } = new();
    public WearableDto? Wearable { get; set; }
    public ViajeDto? ActiveTrip { get; set; }
    public IReadOnlyList<VehicleDto> Vehicles { get; set; } = [];
    public EmergencyContactSyncResponse EmergencyContacts { get; set; } = new();
    public IReadOnlyList<MonitoringRelationshipDto> MonitoringRelationships { get; set; } = [];
    public IReadOnlyList<IncidenteListItemDto> ActiveIncidents { get; set; } = [];
    public IReadOnlyList<QuickMessageTemplateDto> QuickMessageTemplates { get; set; } = [];
    public IReadOnlyList<QuickMessageRecipientDto> QuickMessageRecipients { get; set; } = [];
    public int UnreadNotifications { get; set; }
    public int UnreadQuickMessages { get; set; }
    public MobileOfflineContractDto OfflineContract { get; set; } = new();
}

public sealed class MobileOfflineContractDto
{
    public int ContractVersion { get; set; } = 2;
    public int TelemetrySchemaVersion { get; set; } = 2;
    public int MaxTelemetryEventsPerBatch { get; set; }
    public long MaxTelemetryBodyBytes { get; set; }
    public int MaxOperationsPerPush { get; set; } = 50;
    public string TelemetryWriter { get; set; } = "wearable";
    public bool MobileMayControlTrip { get; set; }
    public bool MobileMayReadTripState { get; set; } = true;
    public bool MobileMayRelayOfflineAlerts { get; set; } = true;
    public string IdempotencyKey { get; set; } = "operationId/eventId";
    public string DuplicateBehavior { get; set; } = "same-operation-is-idempotent;telemetry-different-content-is-409";
    public IReadOnlyList<string> SupportedPushOperations { get; set; } =
    [
        "notification.mark-read",
        "notification.mark-all-read",
        "quick-message.mark-read",
        "permissions.update",
        "fcm-token.upsert",
        "fcm-token.delete",
        "profile.update",
        "preferences.update",
        "onboarding.update",
        "vehicle.create",
        "vehicle.update",
        "vehicle.delete",
        "vehicle.set-primary",
        "quick-message.send"
    ];
}

public sealed class MobileSyncChangesDto
{
    public string Cursor { get; set; } = string.Empty;
    public bool HasChanges { get; set; }
    public bool RequiresBootstrap { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
    public MobileSyncSnapshotDto? Snapshot { get; set; }
}

public sealed class MobileSyncPushRequest
{
    [Required]
    [MaxLength(100)]
    public string ClientInstanceId { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? BaseCursor { get; set; }

    [Required]
    [MinLength(1)]
    [MaxLength(50)]
    public List<MobileSyncOperationDto> Operations { get; set; } = [];
}

public sealed class MobileSyncOperationDto
{
    public Guid OperationId { get; set; }

    [Required]
    [MaxLength(80)]
    public string Type { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
    public JsonElement Payload { get; set; }
}

public sealed class MobileSyncOperationResultDto
{
    public Guid OperationId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string? Message { get; set; }
    public DateTime ProcessedAtUtc { get; set; }
    public bool WasDuplicate { get; set; }
}

public sealed class MobileSyncPushResponse
{
    public string PreviousCursor { get; set; } = string.Empty;
    public string Cursor { get; set; } = string.Empty;
    public bool RequiresPull { get; set; }
    public long ServerRevision { get; set; }
    public IReadOnlyList<MobileSyncOperationResultDto> Results { get; set; } = [];
}

public sealed class MobileSyncAckRequest
{
    [Required]
    [MaxLength(100)]
    public string ClientInstanceId { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string Cursor { get; set; } = string.Empty;
}

public sealed class MobileSyncAckResponse
{
    public string Cursor { get; set; } = string.Empty;
    public DateTime AcknowledgedAtUtc { get; set; }
    public long ServerRevision { get; set; }
}
