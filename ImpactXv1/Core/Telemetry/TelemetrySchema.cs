namespace ImpactX.Core.Telemetry;

/// <summary>
/// Versiones del contrato de telemetría. La versión 1 conserva el payload
/// mínimo histórico. La versión 2 agrega procedencia del Galaxy Watch 8,
/// orden offline y sensores útiles para detección y futuro entrenamiento ML.
/// </summary>
public static class TelemetrySchema
{
    public const int LegacyVersion = 1;
    public const int EnrichedVersion = 2;
    public const int CurrentVersion = EnrichedVersion;

    public const string QualityUnknown = "unknown";
    public const string QualityLow = "low";
    public const string QualityMedium = "medium";
    public const string QualityHigh = "high";

    public static bool IsSupported(int version)
        => version is LegacyVersion or EnrichedVersion;

    public static string? NormalizeQuality(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim().ToLowerInvariant() switch
        {
            QualityUnknown => QualityUnknown,
            QualityLow => QualityLow,
            QualityMedium => QualityMedium,
            QualityHigh => QualityHigh,
            _ => null,
        };
    }
}
