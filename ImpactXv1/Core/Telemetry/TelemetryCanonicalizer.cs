using ImpactX.Models.DTOs;

namespace ImpactX.Core.Telemetry;

/// <summary>
/// Canonicaliza campos derivados antes de persistirlos y compararlos para
/// idempotencia. Un reenvío puede omitir una magnitud derivable sin convertir
/// el mismo EventId en un conflicto.
/// </summary>
public static class TelemetryCanonicalizer
{
    public static double? ResolveMagnitude(
        double? x,
        double? y,
        double? z,
        double? suppliedMagnitude)
    {
        // Cuando existen los tres ejes, la magnitud se calcula en servidor para
        // evitar inconsistencias o manipulación del campo derivado. El valor
        // suministrado solo se conserva para documentos/payloads legacy sin
        // los tres ejes disponibles.
        if (x is not null && y is not null && z is not null)
            return Math.Sqrt((x.Value * x.Value) + (y.Value * y.Value) + (z.Value * z.Value));

        return suppliedMagnitude;
    }

    public static string? NormalizeSensorFlags(IEnumerable<string>? flags)
    {
        if (flags is null)
            return null;

        var normalized = flags
            .Where(flag => !string.IsNullOrWhiteSpace(flag))
            .Select(flag => flag.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(flag => flag, StringComparer.Ordinal)
            .ToArray();

        return normalized.Length == 0 ? null : string.Join(',', normalized);
    }

    public static List<string> ParseSensorFlags(string? csv)
        => string.IsNullOrWhiteSpace(csv)
            ? []
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(flag => flag, StringComparer.Ordinal)
                .ToList();

    public static string? NormalizeOptionalText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static double? ResolveAccelerationMagnitude(TelemetryEventRequest request)
        => ResolveMagnitude(
            request.AceleracionX,
            request.AceleracionY,
            request.AceleracionZ,
            request.MagnitudAceleracion);

    public static double? ResolveGyroscopeMagnitude(TelemetryEventRequest request)
        => ResolveMagnitude(
            request.GiroscopioX,
            request.GiroscopioY,
            request.GiroscopioZ,
            request.MagnitudGiroscopio);
}
