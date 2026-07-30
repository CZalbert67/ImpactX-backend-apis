using System.ComponentModel.DataAnnotations;

namespace ImpactX.Models.DTOs;

public sealed class InvitationTokenRequest
{
    [Required]
    [StringLength(256, MinimumLength = 8)]
    public string Token { get; init; } = string.Empty;
}
