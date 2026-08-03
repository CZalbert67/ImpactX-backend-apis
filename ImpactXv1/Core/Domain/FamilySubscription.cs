using System.Text.Json.Serialization;
using ImpactX.Core.Domain.Enums;

namespace ImpactX.Core.Domain;

public class FamilySubscription
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [JsonPropertyName("publicSubscriptionId")]
    public string PublicSubscriptionId { get; set; } = string.Empty;

    [JsonPropertyName("ownerUserId")]
    public Guid OwnerUserId { get; set; }

    [JsonPropertyName("planName")]
    public string PlanName { get; set; } = "Free";

    [JsonPropertyName("status")]
    public FamilySubscriptionStatus Status { get; set; } = FamilySubscriptionStatus.Active;

    [JsonPropertyName("pendingAdjustment")]
    public bool PendingAdjustment { get; set; }

    [JsonPropertyName("pendingPlanName")]
    public string? PendingPlanName { get; set; }

    [JsonPropertyName("periodStartUtc")]
    public DateTime PeriodStartUtc { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("periodEndUtc")]
    public DateTime PeriodEndUtc { get; set; } = DateTime.UtcNow.AddMonths(1);

    [JsonPropertyName("nextBillingAtUtc")]
    public DateTime? NextBillingAtUtc { get; set; }

    [JsonPropertyName("graceEndsAtUtc")]
    public DateTime? GraceEndsAtUtc { get; set; }

    [JsonPropertyName("autoRenew")]
    public bool AutoRenew { get; set; }

    [JsonPropertyName("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("updatedAtUtc")]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("memberships")]
    public List<FamilyMembership> Memberships { get; set; } = [];

    [JsonPropertyName("invitations")]
    public List<FamilyInvitation> Invitations { get; set; } = [];

    [JsonPropertyName("payments")]
    public List<SimulatedPaymentRecord> Payments { get; set; } = [];

    [Newtonsoft.Json.JsonProperty("_etag")]
    [JsonPropertyName("_etag")]
    public string? ETag { get; set; }
}

public class FamilyMembership
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [JsonPropertyName("publicMembershipId")]
    public string PublicMembershipId { get; set; } = string.Empty;

    [JsonPropertyName("userId")]
    public Guid UserId { get; set; }

    [JsonPropertyName("role")]
    public FamilyMembershipRole Role { get; set; }

    [JsonPropertyName("status")]
    public FamilyMembershipStatus Status { get; set; }

    [JsonPropertyName("invitedAtUtc")]
    public DateTime? InvitedAtUtc { get; set; }

    [JsonPropertyName("acceptedAtUtc")]
    public DateTime? AcceptedAtUtc { get; set; }

    [JsonPropertyName("endedAtUtc")]
    public DateTime? EndedAtUtc { get; set; }

    [JsonPropertyName("publicProfileIdSnapshot")]
    public string PublicProfileIdSnapshot { get; set; } = string.Empty;

    [JsonPropertyName("usernameSnapshot")]
    public string UsernameSnapshot { get; set; } = string.Empty;

    [JsonPropertyName("displayNameSnapshot")]
    public string DisplayNameSnapshot { get; set; } = string.Empty;
}

public class FamilyInvitation
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [JsonPropertyName("publicInvitationId")]
    public string PublicInvitationId { get; set; } = string.Empty;

    [JsonPropertyName("targetUserId")]
    public Guid? TargetUserId { get; set; }

    [JsonPropertyName("targetEmailNormalized")]
    public string? TargetEmailNormalized { get; set; }

    [JsonPropertyName("targetPublicProfileId")]
    public string? TargetPublicProfileId { get; set; }

    [JsonPropertyName("targetUsername")]
    public string? TargetUsername { get; set; }

    [JsonPropertyName("codeHash")]
    public string CodeHash { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public FamilyInvitationStatus Status { get; set; } = FamilyInvitationStatus.Pending;

    [JsonPropertyName("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("expiresAtUtc")]
    public DateTime ExpiresAtUtc { get; set; }

    [JsonPropertyName("respondedAtUtc")]
    public DateTime? RespondedAtUtc { get; set; }

    [JsonPropertyName("consumedAtUtc")]
    public DateTime? ConsumedAtUtc { get; set; }

    [JsonPropertyName("createMonitoringRelationship")]
    public bool CreateMonitoringRelationship { get; set; } = true;
}

public class SimulatedPaymentRecord
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [JsonPropertyName("publicPaymentId")]
    public string PublicPaymentId { get; set; } = string.Empty;

    [JsonPropertyName("result")]
    public string Result { get; set; } = "Approved";

    [JsonPropertyName("planName")]
    public string PlanName { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "MXN";

    [JsonPropertyName("occurredAtUtc")]
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}
