using System.ComponentModel.DataAnnotations;
using ImpactX.Core.Domain.Enums;

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

    public bool CreateMonitoringRelationship { get; set; } = true;
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
    public int AvailableMemberSlots { get; set; }
    public int VehicleLimitPerUser { get; set; }
    public bool PendingAdjustment { get; set; }
    public string? PendingPlanName { get; set; }
    public DateTime PeriodStartUtc { get; set; }
    public DateTime PeriodEndUtc { get; set; }
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
