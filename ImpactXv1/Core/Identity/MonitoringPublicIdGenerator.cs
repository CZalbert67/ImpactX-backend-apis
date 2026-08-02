using System.Security.Cryptography;

namespace ImpactX.Core.Identity;

public static class MonitoringPublicIdGenerator
{
    public static string GenerateRelationshipId()
    {
        var bytes = RandomNumberGenerator.GetBytes(16);
        var encoded = Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
        return "REL-" + encoded;
    }
}
