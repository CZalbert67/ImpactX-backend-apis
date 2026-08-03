using System.Security.Cryptography;

namespace ImpactX.Core.Identity;

public static class EmergencyContactPublicIdGenerator
{
    public static string Generate()
    {
        var encoded = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
        return "ECT-" + encoded;
    }
}
