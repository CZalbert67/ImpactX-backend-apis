using System.Security.Cryptography;
using System.Text;

namespace ImpactX.Core.Identity;

public static class FamilyPublicIdGenerator
{
    public static string GenerateSubscriptionId() => Generate("SUB-");
    public static string GenerateMembershipId() => Generate("MEM-");
    public static string GenerateInvitationId() => Generate("INV-");
    public static string GeneratePaymentId() => Generate("PAY-");

    public static string GenerateManualCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var bytes = RandomNumberGenerator.GetBytes(10);
        var builder = new StringBuilder(12);
        for (var index = 0; index < bytes.Length; index++)
        {
            if (index is 5)
            {
                builder.Append('-');
            }

            builder.Append(alphabet[bytes[index] % alphabet.Length]);
        }

        return builder.ToString();
    }

    private static string Generate(string prefix)
    {
        var bytes = RandomNumberGenerator.GetBytes(16);
        var encoded = Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
        return prefix + encoded;
    }
}
