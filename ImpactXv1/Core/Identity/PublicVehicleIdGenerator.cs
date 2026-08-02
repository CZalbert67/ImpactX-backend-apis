using System.Security.Cryptography;

namespace ImpactX.Core.Identity;

public static class PublicVehicleIdGenerator
{
    public const string Prefix = "VEH-";

    public static string Generate()
    {
        var bytes = new byte[16];
        RandomNumberGenerator.Fill(bytes);
        return Prefix + Base64UrlEncode(bytes);
    }

    private static string Base64UrlEncode(byte[] data)
        => Convert.ToBase64String(data)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
}
