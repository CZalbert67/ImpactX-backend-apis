using System.ComponentModel.DataAnnotations;
using ImpactX.Core.Domain.Enums;
using ImpactX.Models.DTOs.Monitoring;

namespace ImpactX.Models.DTOs.FamilySubscriptions;

public class ActivateFamilySubscriptionRequest
{
    [Required, MaxLength(30)]
    public string PlanName { get; set; } = string.Empty;
}

public class ChangeFamilyPlanRequest
{
    [Required, MaxLength(30)]
    public string PlanName { get; set; } = string.Empty;
}

public class CreateFamilyInvitationRequest
{
    [MaxLength(50)]
    public string? Username { get; set; }

    [MaxLength(64)]
    public string? PublicProfileId { get; set; }

    [EmailAddress, MaxLength(256)]
    public string? Email { get; set; }

    public bool CreateMonitoringRelationship { get; set; }
}

public class RedeemFamilyInvitationRequest
{
    [Required, MaxLength(64)]
    public string Code { get; set; } = string.Empty;
}

public class FamilySubscriptionSummaryDto
{
    public string PublicSubscriptionId { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public FamilySubscriptionStatus Status { get; set; }
    public FamilyMembershipRole CurrentUserRole { get; set; }
    public string OwnerPublicProfileId { get; set; } = string.Empty;
    public string OwnerUsername { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public int AcceptedMembers { get; set; }
    public int InvitedMemberLimit { get; set; }
    public int TotalActivePeople { get; set; }
    public int TotalPeopleLimit { get; set; }
    public int PendingInvitationCount { get; set; }
    public int AvailableMemberSlots { get; set; }
    public int VehicleLimitPerUser { get; set; }
    public bool PendingAdjustment { get; set; }
    public string? PendingPlanName { get; set; }
    public DateTime PeriodStartUtc { get; set; }
    public DateTime PeriodEndUtc { get; set; }
    public DateTime? NextBillingAtUtc { get; set; }
    public DateTime? GraceEndsAtUtc { get; set; }
    public bool AutoRenew { get; set; }
    public bool CanManagePlan { get; set; }
    public bool CanInviteMembers { get; set; }
    public bool CanLeaveGroup { get; set; }
    public int SosContactLimit { get; set; }
    public SimulatedPaymentDto? LatestPayment { get; set; }
}

public class FamilyMemberDto
{
    public string PublicMembershipId { get; set; } = string.Empty;
    public string PublicProfileId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public FamilyMembershipRole Role { get; set; }
    public FamilyMembershipStatus Status { get; set; }
    public DateTime? AcceptedAtUtc { get; set; }
}

public class FamilyInvitationDto
{
    public string PublicInvitationId { get; set; } = string.Empty;
    public string? TargetUsername { get; set; }
    public string? TargetPublicProfileId { get; set; }
    public string? TargetEmail { get; set; }
    public FamilyInvitationStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}

public class IncomingFamilyInvitationDto : FamilyInvitationDto
{
    public string OwnerPublicProfileId { get; set; } = string.Empty;
    public string OwnerUsername { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
}

public class CreateFamilyInvitationResponse
{
    public FamilyInvitationDto Invitation { get; set; } = new();
    public string ManualCode { get; set; } = string.Empty;
}

public class SimulatedPaymentDto
{
    public string PublicPaymentId { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
}


public class FamilyMemberAccessDto
{
    public string PublicRelationshipId { get; set; } = string.Empty;
    public string PublicSubscriptionId { get; set; } = string.Empty;
    public string SubjectPublicProfileId { get; set; } = string.Empty;
    public string SubjectUsername { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public string ViewerPublicProfileId { get; set; } = string.Empty;
    public string ViewerUsername { get; set; } = string.Empty;
    public string ViewerName { get; set; } = string.Empty;
    public MonitoringPermissionsDto Permissions { get; set; } = new();
    public bool MedicalConsentGranted { get; set; }
    public int? SosPriority { get; set; }
    public bool IsSosContact => SosPriority.HasValue;
    public DateTime UpdatedAtUtc { get; set; }
}

public class UpdateFamilyMemberAccessRequest
{
    public bool ViewRoutes { get; set; }
    public bool ViewLocation { get; set; }
    public bool ViewEmergencyLocation { get; set; } = true;
    public bool ViewIncidents { get; set; } = true;
    public bool ReceiveCriticalAlerts { get; set; } = true;
    public bool ViewMedicalProfile { get; set; }
    public bool SendMessages { get; set; } = true;
    public bool ViewTelemetry { get; set; }
    public bool ReceiveNotifications { get; set; } = true;
    public bool ConfirmMedicalConsent { get; set; }
    public int? SosPriority { get; set; }
}
