namespace ImpactX.Core.Wearables;

/// <summary>
/// Contrato técnico del wearable objetivo del prototipo ImpactX.
/// La validación vive en backend para que un cliente no pueda registrar un
/// dispositivo distinto ocultando controles en la interfaz.
/// </summary>
public static class WearableProductPolicy
{
    public const string TargetManufacturer = "Samsung";
    public const string TargetModel = "Galaxy Watch 8";
    public const string TargetPlatform = "WearOS";

    public const int MaxDeviceIdLength = 200;
    public const int MaxNameLength = 200;
    public const int MaxVersionLength = 80;
    public const int MaxSensorCapabilityLength = 64;
    public const int MaxSensorCapabilities = 32;

    public static readonly TimeSpan PairingLifetime = TimeSpan.FromMinutes(10);

    public static bool IsTargetDevice(string? manufacturer, string? model, string? platform)
        => string.Equals(manufacturer?.Trim(), TargetManufacturer, StringComparison.OrdinalIgnoreCase)
           && string.Equals(model?.Trim(), TargetModel, StringComparison.OrdinalIgnoreCase)
           && string.Equals(platform?.Trim(), TargetPlatform, StringComparison.OrdinalIgnoreCase);

    public static string NormalizeManufacturer(string? value)
        => string.IsNullOrWhiteSpace(value) ? TargetManufacturer : value.Trim();

    public static string NormalizePlatform(string? value)
        => string.IsNullOrWhiteSpace(value) ? TargetPlatform : value.Trim();

    public static string NormalizeModel(string? value)
        => value?.Trim() ?? string.Empty;

    public static IReadOnlyList<string> NormalizeCapabilities(IEnumerable<string>? values)
    {
        if (values is null)
            return [];

        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .Take(MaxSensorCapabilities)
            .ToList();
    }
}
