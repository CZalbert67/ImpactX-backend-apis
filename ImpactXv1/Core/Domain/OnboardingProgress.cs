using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace ImpactX.Core.Domain;

public enum OnboardingStatus
{
    Pending = 0,
    Completed = 1
}

public enum MedicalProfileOnboardingStatus
{
    Pending = 0,
    Completed = 1,
    Skipped = 2
}

public class OnboardingProgress
{
    public const int MinCurrentStep = 1;
    public const int MaxCurrentStep = 8;

    [JsonProperty("status")]
    [JsonPropertyName("status")]
    public OnboardingStatus Status { get; set; } = OnboardingStatus.Pending;

    [JsonProperty("currentStep")]
    [JsonPropertyName("currentStep")]
    public int CurrentStep { get; set; } = MinCurrentStep;

    [JsonProperty("medicalProfileStatus")]
    [JsonPropertyName("medicalProfileStatus")]
    public MedicalProfileOnboardingStatus MedicalProfileStatus { get; set; } = MedicalProfileOnboardingStatus.Pending;

    [JsonProperty("privacyAccepted")]
    [JsonPropertyName("privacyAccepted")]
    public bool PrivacyAccepted { get; set; }

    [JsonProperty("locationIncidentConsent")]
    [JsonPropertyName("locationIncidentConsent")]
    public bool LocationIncidentConsent { get; set; }

    [JsonProperty("drivingPatternConsent")]
    [JsonPropertyName("drivingPatternConsent")]
    public bool DrivingPatternConsent { get; set; }

    [JsonProperty("completedAtUtc")]
    [JsonPropertyName("completedAtUtc")]
    public DateTime? CompletedAtUtc { get; set; }

    [JsonProperty("updatedAtUtc")]
    [JsonPropertyName("updatedAtUtc")]
    public DateTime? UpdatedAtUtc { get; set; }
}
