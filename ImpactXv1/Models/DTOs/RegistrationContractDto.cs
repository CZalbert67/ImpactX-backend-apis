namespace ImpactX.Models.DTOs;

public sealed class RegistrationContractDto
{
    public int ContractVersion { get; init; }
    public string TermsVersion { get; init; } = string.Empty;
    public string PrivacyNoticeVersion { get; init; } = string.Empty;
    public IReadOnlyList<string> SupportedClients { get; init; } = [];
    public IReadOnlyList<string> RequiredFields { get; init; } = [];
    public UsernameRequirementsDto Username { get; init; } = new();
    public PasswordRequirementsDto Password { get; init; } = new();
    public bool ConfirmPasswordIsClientOnly { get; init; } = true;
}

public sealed class UsernameRequirementsDto
{
    public int MinLength { get; init; }
    public int MaxLength { get; init; }
    public string Pattern { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}

public sealed class PasswordRequirementsDto
{
    public int MinLength { get; init; }
    public int MaxLength { get; init; }
    public bool RequireUppercase { get; init; }
    public bool RequireLowercase { get; init; }
    public bool RequireDigit { get; init; }
    public bool RequireSpecialCharacter { get; init; }
}
