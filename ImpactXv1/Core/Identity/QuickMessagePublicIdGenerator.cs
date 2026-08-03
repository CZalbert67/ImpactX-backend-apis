using System.Security.Cryptography;

namespace ImpactX.Core.Identity;

public static class QuickMessagePublicIdGenerator
{
    public static string GenerateTemplateId() => Generate("QMT-");
    public static string GenerateMessageId() => Generate("QMS-");

    private static string Generate(string prefix)
    {
        var encoded = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
        return prefix + encoded;
    }
}
