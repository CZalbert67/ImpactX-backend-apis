using System.ComponentModel.DataAnnotations;
using ImpactX.Core.Identity;

namespace ImpactX.Models.DTOs;

public class RegisterRequest
{
    [Range(RegistrationContract.LegacyVersion, RegistrationContract.CurrentVersion)]
    public int RegistrationVersion { get; set; } = RegistrationContract.LegacyVersion;

    [Required, MaxLength(200)]
    public string Nombre { get; set; } = string.Empty;

    [MaxLength(UsernamePolicy.MaxLength)]
    public string? Username { get; set; }

    [Required, MaxLength(256), EmailAddress]
    public string Correo { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Telefono { get; set; }

    [Required, MinLength(RegistrationContract.PasswordMinLength), MaxLength(RegistrationContract.PasswordMaxLength)]
    public string Password { get; set; } = string.Empty;

    public bool? TermsAccepted { get; set; }
    public bool? PrivacyAccepted { get; set; }
    public bool? LocationIncidentConsent { get; set; }
    public bool? DrivingPatternConsent { get; set; }

    public string? PlanActivo { get; set; }

    [RegularExpression("^(web|mobile|wearable)$", ErrorMessage = "El cliente debe ser web, mobile o wearable.")]
    public string Client { get; set; } = "mobile";
}
