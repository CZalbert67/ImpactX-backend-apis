using System.Security.Cryptography;
using System.Text;

namespace ImpactX.Core.Security;

public static class InvitationCodeHasher
{
    public static string Hash(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return string.Empty;
        }

        var normalized = Normalize(code);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))
            .ToLowerInvariant();
    }

    public static string Normalize(string code)
        => code.Trim().ToUpperInvariant();
}
