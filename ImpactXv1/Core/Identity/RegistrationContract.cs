namespace ImpactX.Core.Identity;

/// <summary>
/// Contrato público y versionado del registro de cuentas de ImpactX.
/// Las versiones legales son controladas por el servidor; el cliente solo
/// confirma que mostró y aceptó las versiones publicadas por este contrato.
/// </summary>
public static class RegistrationContract
{
    public const int LegacyVersion = 1;
    public const int CurrentVersion = 2;

    public const string TermsVersion = "1.0-2026-08-03";
    public const string PrivacyNoticeVersion = "1.0-2026-08-03";

    public const int PasswordMinLength = 8;
    public const int PasswordMaxLength = 100;

    public static IReadOnlyList<string> SupportedAccountClients { get; } =
        Array.AsReadOnly(new[] { "web", "mobile" });

    public static bool IsStrongPassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password)
            || password.Length is < PasswordMinLength or > PasswordMaxLength)
        {
            return false;
        }

        return password.Any(char.IsUpper)
            && password.Any(char.IsLower)
            && password.Any(char.IsDigit)
            && password.Any(character => !char.IsLetterOrDigit(character));
    }

    public static bool IsValidPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return false;
        }

        var trimmed = phone.Trim();
        if (trimmed.Length > 20)
        {
            return false;
        }

        var digitCount = 0;
        foreach (var character in trimmed)
        {
            if (char.IsDigit(character))
            {
                digitCount++;
                continue;
            }

            if (character is not ('+' or ' ' or '-' or '(' or ')'))
            {
                return false;
            }
        }

        return digitCount is >= 7 and <= 15;
    }
}
