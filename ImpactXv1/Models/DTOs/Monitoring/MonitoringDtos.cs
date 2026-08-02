using System.ComponentModel.DataAnnotations;
using ImpactX.Core.Domain.Enums;

namespace ImpactX.Models.DTOs.Monitoring;

public class CreateMonitoringInvitationRequest
{
    [MaxLength(50)]
    public string? Username { get; set; }

    [MaxLength(64)]
    public string? PublicProfileId { get; set; }

    [EmailAddress, MaxLength(256)]
    public string? Email { get; set; }

    public MonitoringPermissionsRequest? Permissions { get; set; }
}

public class AcceptMonitoringInvitationRequest
{
    [MaxLength(40)]
    public string? PublicRelationshipId { get; set; }

    [MaxLength(64)]
    public string? Code { get; set; }
}

public class RespondMonitoringInvitationRequest
{
    [MaxLength(40)]
    public string? PublicRelationshipId { get; set; }

    [MaxLength(64)]
    public string? Code { get; set; }
}

public class UpdateMonitoringPermissionsRequest
{
    public bool ViewRoutes { get; set; }
    public bool ViewLocation { get; set; }
    public bool ViewEmergencyLocation { get; set; }
    public bool ViewIncidents { get; set; }
    public bool ReceiveCriticalAlerts { get; set; }
    public bool ViewMedicalProfile { get; set; }
    public bool SendMessages { get; set; }
    public bool ViewTelemetry { get; set; }
    public bool ReceiveNotifications { get; set; }
    public bool ConfirmMedicalConsent { get; set; }
}

public class MonitoringPermissionsRequest
{
    public bool ViewRoutes { get; set; } = true;
    public bool ViewLocation { get; set; } = true;
    public bool ViewEmergencyLocation { get; set; } = true;
    public bool ViewIncidents { get; set; } = true;
    public bool ReceiveCriticalAlerts { get; set; } = true;
    public bool SendMessages { get; set; } = true;
    public bool ViewTelemetry { get; set; } = true;
    public bool ReceiveNotifications { get; set; } = true;
}

public class MonitoringRelationshipDto
{
    public string PublicRelationshipId { get; set; } = string.Empty;
    public MonitoringRelationshipStatus Status { get; set; }
    public MonitoringRequestDirection Direction { get; set; }
    public string MonitorPublicProfileId { get; set; } = string.Empty;
    public string MonitorUsername { get; set; } = string.Empty;
    public string MonitorName { get; set; } = string.Empty;
    public string? MonitoredPublicProfileId { get; set; }
    public string? MonitoredUsername { get; set; }
    public string? MonitoredName { get; set; }
    public MonitoringPermissionsDto Permissions { get; set; } = new();
    public DateTime RequestedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? AcceptedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
}

public class MonitoringPermissionsDto
{
    public bool ViewRoutes { get; set; }
    public bool ViewLocation { get; set; }
    public bool ViewEmergencyLocation { get; set; }
    public bool ViewIncidents { get; set; }
    public bool ReceiveCriticalAlerts { get; set; }
    public bool ViewMedicalProfile { get; set; }
    public bool SendMessages { get; set; }
    public bool ViewTelemetry { get; set; }
    public bool ReceiveNotifications { get; set; }
}

public class CreateMonitoringInvitationResponse
{
    public MonitoringRelationshipDto Relationship { get; set; } = new();
    public string ManualCode { get; set; } = string.Empty;
}
