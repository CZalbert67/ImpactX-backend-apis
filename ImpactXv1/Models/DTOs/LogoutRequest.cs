using System.ComponentModel.DataAnnotations;

namespace ImpactX.Models.DTOs;

public class LogoutRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}
