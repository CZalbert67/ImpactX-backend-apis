using ImpactX.Core.Exceptions;
using ImpactX.Core.Wearables;
using ImpactX.Models.DTOs;

namespace ImpactX.Core.Telemetry;

/// <summary>
/// Validación centralizada del contrato de ingesta de telemetría por lotes.
/// La versión 1 mantiene compatibilidad con el payload mínimo histórico; la
/// versión 2 exige procedencia del Galaxy Watch 8, secuencia y sensores de
/// movimiento suficientes para sincronización offline y futuro análisis ML.
/// </summary>
public static class TelemetryBatchValidator
{
    public static void Validate(TelemetryBatchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!TelemetrySchema.IsSupported(request.SchemaVersion))
            throw new BadRequestException("La versión del esquema de telemetría no es compatible.");

        ValidateBatchMetadata(request);

        if (request.Eventos is null || request.Eventos.Count < TelemetryIngestionLimits.MinEventsPerBatch)
            throw new BadRequestException($"El lote debe contener al menos {TelemetryIngestionLimits.MinEventsPerBatch} evento.");

        if (request.Eventos.Count > TelemetryIngestionLimits.MaxEventsPerBatch)
            throw new BadRequestException($"El lote no puede superar los {TelemetryIngestionLimits.MaxEventsPerBatch} eventos.");

        var eventIds = new HashSet<Guid>(request.Eventos.Count);
        var sequences = new HashSet<long>();

        foreach (var evento in request.Eventos)
        {
            if (evento.EventId == Guid.Empty)
                throw new BadRequestException("Cada evento debe incluir un EventId no vacío.");

            if (!eventIds.Add(evento.EventId))
                throw new BadRequestException("El EventId no puede repetirse dentro del mismo lote.");

            if (request.SchemaVersion >= TelemetrySchema.EnrichedVersion && evento.SequenceNumber is null)
                throw new BadRequestException("Cada evento del esquema v2 debe incluir sequenceNumber.");

            if (evento.SequenceNumber is < 0)
                throw new BadRequestException("sequenceNumber no puede ser negativo.");

            if (evento.SequenceNumber is long sequence && !sequences.Add(sequence))
                throw new BadRequestException("sequenceNumber no puede repetirse dentro del mismo lote.");

            ValidateEvento(evento, request.SchemaVersion);
        }
    }

    private static void ValidateBatchMetadata(TelemetryBatchRequest request)
    {
        if (request.BatchId == Guid.Empty)
            throw new BadRequestException("batchId no puede ser un GUID vacío.");

        if (request.BatchSequence is < 0)
            throw new BadRequestException("batchSequence no puede ser negativo.");

        if (request.BatteryLevel is < 0 or > 100)
            throw new BadRequestException("batteryLevel debe estar entre 0 y 100.");

        if (request.ClockOffsetMilliseconds is < TelemetryIngestionLimits.MinClockOffsetMilliseconds or > TelemetryIngestionLimits.MaxClockOffsetMilliseconds)
            throw new BadRequestException("clockOffsetMilliseconds está fuera del rango permitido.");

        ValidateText(request.WearableDeviceId, "wearableDeviceId", TelemetryIngestionLimits.MaxDeviceMetadataLength);
        ValidateText(request.WearableModel, "wearableModel", TelemetryIngestionLimits.MaxDeviceMetadataLength);
        ValidateText(request.WearableAppVersion, "wearableAppVersion", TelemetryIngestionLimits.MaxVersionLength);
        ValidateText(request.WearableOsVersion, "wearableOsVersion", TelemetryIngestionLimits.MaxVersionLength);
        ValidateText(request.WearableFirmwareVersion, "wearableFirmwareVersion", TelemetryIngestionLimits.MaxVersionLength);

        if (request.SchemaVersion < TelemetrySchema.EnrichedVersion)
            return;

        if (request.BatchId is null)
            throw new BadRequestException("El esquema v2 requiere batchId.");

        if (request.BatchSequence is null)
            throw new BadRequestException("El esquema v2 requiere batchSequence.");

        if (string.IsNullOrWhiteSpace(request.WearableDeviceId))
            throw new BadRequestException("El esquema v2 requiere wearableDeviceId.");

        if (!string.Equals(request.WearableModel?.Trim(), WearableProductPolicy.TargetModel, StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("El esquema v2 solo admite el modelo wearable objetivo.");

        if (request.BatteryLevel is null)
            throw new BadRequestException("El esquema v2 requiere batteryLevel.");
    }

    private static void ValidateEvento(TelemetryEventRequest evento, int schemaVersion)
    {
        if (evento.Timestamp == default || evento.Timestamp.Kind != DateTimeKind.Utc)
            throw new BadRequestException("El timestamp de cada evento debe estar en UTC.");

        if (evento.Timestamp > DateTime.UtcNow.Add(TelemetryIngestionLimits.MaxFutureTolerance))
            throw new BadRequestException("El timestamp de un evento no puede estar más de 5 minutos en el futuro.");

        ValidateRequiredLocation(evento);
        ValidateOptionalSensors(evento);

        if (schemaVersion < TelemetrySchema.EnrichedVersion)
            return;

        if (evento.GpsAccuracyMeters is null)
            throw new BadRequestException("El esquema v2 requiere gpsAccuracyMeters.");

        if (evento.AceleracionX is null || evento.AceleracionY is null || evento.AceleracionZ is null)
            throw new BadRequestException("El esquema v2 requiere aceleración X, Y y Z.");

        if (evento.GiroscopioX is null || evento.GiroscopioY is null || evento.GiroscopioZ is null)
            throw new BadRequestException("El esquema v2 requiere giroscopio X, Y y Z.");

        if (TelemetrySchema.NormalizeQuality(evento.CalidadSensor) is null)
            throw new BadRequestException("El esquema v2 requiere calidadSensor válida.");
    }

    private static void ValidateRequiredLocation(TelemetryEventRequest evento)
    {
        RequireFinite(evento.Lat, "latitud");
        RequireFinite(evento.Lng, "longitud");
        RequireFinite(evento.Velocidad, "velocidad");

        ValidateRange(evento.Lat, TelemetryIngestionLimits.MinLatitude, TelemetryIngestionLimits.MaxLatitude, "La latitud debe estar entre -90 y 90 grados.");
        ValidateRange(evento.Lng, TelemetryIngestionLimits.MinLongitude, TelemetryIngestionLimits.MaxLongitude, "La longitud debe estar entre -180 y 180 grados.");
        ValidateRange(evento.Velocidad, TelemetryIngestionLimits.MinSpeedKmh, TelemetryIngestionLimits.MaxSpeedKmh, "La velocidad debe estar entre 0 y 500 km/h.");

        ValidateOptionalRange(evento.Altitud, TelemetryIngestionLimits.MinAltitudeMeters, TelemetryIngestionLimits.MaxAltitudeMeters, "altitud", "La altitud debe estar entre -500 y 10000 metros.");
        ValidateOptionalRange(evento.Heading, TelemetryIngestionLimits.MinHeadingDegrees, TelemetryIngestionLimits.MaxHeadingDegrees, "rumbo", "El rumbo debe estar entre 0 y menos de 360 grados.", maxExclusive: true);
        ValidateOptionalRange(evento.GpsAccuracyMeters, TelemetryIngestionLimits.MinGpsAccuracyMeters, TelemetryIngestionLimits.MaxGpsAccuracyMeters, "precisión GPS", "La precisión GPS debe estar entre 0 y 5000 metros.");
    }

    private static void ValidateOptionalSensors(TelemetryEventRequest evento)
    {
        ValidateOptionalRange(evento.AceleracionX, TelemetryIngestionLimits.MinAccelerationMps2, TelemetryIngestionLimits.MaxAccelerationMps2, "aceleración X", "La aceleración X está fuera del rango permitido.");
        ValidateOptionalRange(evento.AceleracionY, TelemetryIngestionLimits.MinAccelerationMps2, TelemetryIngestionLimits.MaxAccelerationMps2, "aceleración Y", "La aceleración Y está fuera del rango permitido.");
        ValidateOptionalRange(evento.AceleracionZ, TelemetryIngestionLimits.MinAccelerationMps2, TelemetryIngestionLimits.MaxAccelerationMps2, "aceleración Z", "La aceleración Z está fuera del rango permitido.");

        var accelerationMagnitude = TelemetryCanonicalizer.ResolveAccelerationMagnitude(evento);
        ValidateOptionalRange(accelerationMagnitude, TelemetryIngestionLimits.MinAccelerationMagnitudeMps2, TelemetryIngestionLimits.MaxAccelerationMagnitudeMps2, "magnitud de aceleración", "La magnitud de aceleración está fuera del rango permitido.");

        ValidateOptionalRange(evento.GiroscopioX, TelemetryIngestionLimits.MinGyroscopeRadPerSecond, TelemetryIngestionLimits.MaxGyroscopeRadPerSecond, "giroscopio X", "El giroscopio X está fuera del rango permitido.");
        ValidateOptionalRange(evento.GiroscopioY, TelemetryIngestionLimits.MinGyroscopeRadPerSecond, TelemetryIngestionLimits.MaxGyroscopeRadPerSecond, "giroscopio Y", "El giroscopio Y está fuera del rango permitido.");
        ValidateOptionalRange(evento.GiroscopioZ, TelemetryIngestionLimits.MinGyroscopeRadPerSecond, TelemetryIngestionLimits.MaxGyroscopeRadPerSecond, "giroscopio Z", "El giroscopio Z está fuera del rango permitido.");

        var gyroscopeMagnitude = TelemetryCanonicalizer.ResolveGyroscopeMagnitude(evento);
        ValidateOptionalRange(gyroscopeMagnitude, TelemetryIngestionLimits.MinGyroscopeMagnitudeRadPerSecond, TelemetryIngestionLimits.MaxGyroscopeMagnitudeRadPerSecond, "magnitud de giroscopio", "La magnitud de giroscopio está fuera del rango permitido.");

        ValidateOptionalRange(evento.Desaceleracion, TelemetryIngestionLimits.MinDecelerationMps2, TelemetryIngestionLimits.MaxDecelerationMps2, "desaceleración", "La desaceleración está fuera del rango permitido.");

        if (evento.FrecuenciaCardiaca is < TelemetryIngestionLimits.MinHeartRateBpm or > TelemetryIngestionLimits.MaxHeartRateBpm)
            throw new BadRequestException("La frecuencia cardiaca debe estar entre 20 y 250 bpm.");

        ValidateOptionalRange(evento.HrvMilisegundos, TelemetryIngestionLimits.MinHrvMilliseconds, TelemetryIngestionLimits.MaxHrvMilliseconds, "HRV", "HRV debe estar entre 0 y 500 milisegundos.");
        ValidateOptionalRange(evento.Spo2Porcentaje, TelemetryIngestionLimits.MinSpo2Percent, TelemetryIngestionLimits.MaxSpo2Percent, "SpO2", "SpO2 debe estar entre 50 y 100 por ciento.");
        ValidateOptionalRange(evento.Pitch, TelemetryIngestionLimits.MinPitchDegrees, TelemetryIngestionLimits.MaxPitchDegrees, "pitch", "Pitch debe estar entre -180 y 180 grados.");
        ValidateOptionalRange(evento.Roll, TelemetryIngestionLimits.MinRollDegrees, TelemetryIngestionLimits.MaxRollDegrees, "roll", "Roll debe estar entre -180 y 180 grados.");
        ValidateOptionalRange(evento.Yaw, TelemetryIngestionLimits.MinYawDegrees, TelemetryIngestionLimits.MaxYawDegrees, "yaw", "Yaw debe estar entre 0 y menos de 360 grados.", maxExclusive: true);

        if (!string.IsNullOrWhiteSpace(evento.CalidadSensor) && TelemetrySchema.NormalizeQuality(evento.CalidadSensor) is null)
            throw new BadRequestException("calidadSensor debe ser unknown, low, medium o high.");

        if (evento.SensorFlags is null)
            return;

        if (evento.SensorFlags.Count > TelemetryIngestionLimits.MaxSensorFlags)
            throw new BadRequestException($"sensorFlags no puede superar {TelemetryIngestionLimits.MaxSensorFlags} elementos.");

        foreach (var flag in evento.SensorFlags)
        {
            if (string.IsNullOrWhiteSpace(flag) || flag.Trim().Length > TelemetryIngestionLimits.MaxSensorFlagLength)
                throw new BadRequestException("Cada sensorFlag debe contener entre 1 y 64 caracteres.");

            if (flag.Any(char.IsControl) || flag.Contains(','))
                throw new BadRequestException("sensorFlags contiene caracteres no permitidos.");
        }
    }

    private static void ValidateOptionalRange(
        double? value,
        double min,
        double max,
        string field,
        string message,
        bool maxExclusive = false)
    {
        if (value is null)
            return;

        RequireFinite(value.Value, field);
        var invalid = value.Value < min || (maxExclusive ? value.Value >= max : value.Value > max);
        if (invalid)
            throw new BadRequestException(message);
    }

    private static void ValidateRange(double value, double min, double max, string message)
    {
        if (value < min || value > max)
            throw new BadRequestException(message);
    }

    private static void ValidateText(string? value, string field, int maxLength)
    {
        if (value is null)
            return;

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
            throw new BadRequestException($"{field} supera la longitud permitida.");

        if (trimmed.Any(char.IsControl))
            throw new BadRequestException($"{field} contiene caracteres no permitidos.");
    }

    private static void RequireFinite(double value, string field)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new BadRequestException($"El valor de {field} no puede ser NaN o infinito.");
    }
}
