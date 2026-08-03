using System.ComponentModel.DataAnnotations;
using ImpactX.Core.Domain.Enums;

namespace ImpactX.Models.DTOs;

public class CreateEmergencyContactInvitationRequest
{
    [MaxLength(50)]
    public string? Username { get; set; }

    [MaxLength(64)]
    public string? PublicProfileId { get; set; }

    [EmailAddress, MaxLength(256)]
    public string? Email { get; set; }

    [MaxLength(100)]
    public string? Relationship { get; set; }

    [MaxLength(20)]
    public string Priority { get; set; } = "Secondary";

    public bool MakePrimaryWhenAccepted { get; set; }
}

public class RespondEmergencyContactInvitationRequest
{
    [MaxLength(40)]
    public string? PublicContactId { get; set; }

    [MaxLength(64)]
    public string? Code { get; set; }
}

public class UpdateEmergencyContactRequest
{
    [MaxLength(100)]
    public string? Relationship { get; set; }

    [MaxLength(20)]
    public string? Priority { get; set; }
}

public class EmergencyContactDto
{
    public string PublicContactId { get; set; } = string.Empty;
    public EmergencyContactStatus Status { get; set; }
    public bool IsOwner { get; set; }
    public string OwnerPublicProfileId { get; set; } = string.Empty;
    public string OwnerUsername { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string? ContactPublicProfileId { get; set; }
    public string? ContactUsername { get; set; }
    public string? ContactName { get; set; }
    public string? TargetEmailHint { get; set; }
    public string? Relationship { get; set; }
    public string Priority { get; set; } = "Secondary";
    public bool IsPrimary { get; set; }
    public DateTime RequestedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? AcceptedAtUtc { get; set; }
    public DateTime? RejectedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public DateTime? BlockedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public class CreateEmergencyContactInvitationResponse
{
    public EmergencyContactDto Contact { get; set; } = new();
    public string ManualCode { get; set; } = string.Empty;
}

public class EmergencyContactSyncResponse
{
    public IReadOnlyList<EmergencyContactDto> Contacts { get; set; } = [];
    public DateTime SynchronizedAtUtc { get; set; } = DateTime.UtcNow;
}
