using System.Security.Cryptography;

namespace ImpactX.Tests.Integration;

internal static class TestJwtConfiguration
{
    internal static string Secret { get; } =
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
}
