using ImpactX.Core.Domain;

namespace ImpactX.Models.DTOs;

public static class OnboardingDtoMapper
{
    public static OnboardingDto? Map(OnboardingProgress? onboarding)
    {
        if (onboarding is null)
        {
            return null;
        }

        return new OnboardingDto
        {
            Status = onboarding.Status.ToString(),
            CurrentStep = onboarding.CurrentStep,
            MedicalProfileStatus = onboarding.MedicalProfileStatus.ToString(),
            RegistrationContractVersion = onboarding.RegistrationContractVersion,
            TermsAccepted = onboarding.TermsAccepted,
            TermsVersion = onboarding.TermsVersion,
            TermsAcceptedAtUtc = onboarding.TermsAcceptedAtUtc,
            PrivacyAccepted = onboarding.PrivacyAccepted,
            PrivacyNoticeVersion = onboarding.PrivacyNoticeVersion,
            PrivacyAcceptedAtUtc = onboarding.PrivacyAcceptedAtUtc,
            LocationIncidentConsent = onboarding.LocationIncidentConsent,
            DrivingPatternConsent = onboarding.DrivingPatternConsent,
            CompletedAtUtc = onboarding.CompletedAtUtc,
            UpdatedAtUtc = onboarding.UpdatedAtUtc
        };
    }
}
