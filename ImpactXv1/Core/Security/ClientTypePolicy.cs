namespace ImpactX.Core.Security;

public static class ClientTypePolicy
{
    public const string Mobile = "mobile";
    public const string Wearable = "wearable";
    public const string Web = "web";

    public static string Normalize(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return normalized switch
        {
            Web => Web,
            Wearable => Wearable,
            Mobile => Mobile,
            null or "" => Mobile,
            _ => throw new ArgumentException("El cliente debe ser 'web', 'mobile' o 'wearable'.", nameof(value))
        };
    }
}
