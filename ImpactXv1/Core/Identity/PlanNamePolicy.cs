namespace ImpactX.Core.Identity;

public static class PlanNamePolicy
{
    public const string Free = "Free";
    public const string Standard = "Standard";
    public const string LegacyBasic = "Basic";
    public const string Premium = "Premium";

    public static string ToStorageName(string? value)
    {
        var normalized = value?.Trim();
        if (string.Equals(normalized, Standard, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, LegacyBasic, StringComparison.OrdinalIgnoreCase))
            return LegacyBasic;
        if (string.Equals(normalized, Premium, StringComparison.OrdinalIgnoreCase))
            return Premium;
        if (string.Equals(normalized, Free, StringComparison.OrdinalIgnoreCase))
            return Free;
        return normalized ?? string.Empty;
    }

    public static string ToPublicName(string? value)
    {
        return string.Equals(value, LegacyBasic, StringComparison.OrdinalIgnoreCase)
            ? Standard
            : string.IsNullOrWhiteSpace(value) ? Free : value.Trim();
    }
}
