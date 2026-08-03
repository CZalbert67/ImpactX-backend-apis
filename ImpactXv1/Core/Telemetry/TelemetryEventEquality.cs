using ImpactX.Core.Domain;
using ImpactX.Models.DTOs;

namespace ImpactX.Core.Telemetry;

/// <summary>
/// Igualdad idempotente entre un evento persistido y uno recibido. Compara
/// solo el contenido capturado del evento; fechas del servidor y metadatos de
/// reempaquetado del batch no participan.
/// </summary>
public static class TelemetryEventEquality
{
    public static bool IsIdentical(ViajeTelemetry persisted, TelemetryEventRequest received)
        => persisted.Timestamp == received.Timestamp
           && persisted.SequenceNumber == received.SequenceNumber
           && ExactEquals(persisted.Lat, received.Lat)
           && ExactEquals(persisted.Lng, received.Lng)
           && ExactEquals(persisted.Velocidad, received.Velocidad)
           && ExactEquals(persisted.Altitud, received.Altitud)
           && ExactEquals(persisted.Heading, received.Heading)
           && ExactEquals(persisted.GpsAccuracyMeters, received.GpsAccuracyMeters)
           && ExactEquals(persisted.AceleracionX, received.AceleracionX)
           && ExactEquals(persisted.AceleracionY, received.AceleracionY)
           && ExactEquals(persisted.AceleracionZ, received.AceleracionZ)
           && ExactEquals(persisted.MagnitudAceleracion, TelemetryCanonicalizer.ResolveAccelerationMagnitude(received))
           && ExactEquals(persisted.GiroscopioX, received.GiroscopioX)
           && ExactEquals(persisted.GiroscopioY, received.GiroscopioY)
           && ExactEquals(persisted.GiroscopioZ, received.GiroscopioZ)
           && ExactEquals(persisted.MagnitudGiroscopio, TelemetryCanonicalizer.ResolveGyroscopeMagnitude(received))
           && ExactEquals(persisted.Desaceleracion, received.Desaceleracion)
           && persisted.FrecuenciaCardiaca == received.FrecuenciaCardiaca
           && ExactEquals(persisted.HrvMilisegundos, received.HrvMilisegundos)
           && ExactEquals(persisted.Spo2Porcentaje, received.Spo2Porcentaje)
           && ExactEquals(persisted.Pitch, received.Pitch)
           && ExactEquals(persisted.Roll, received.Roll)
           && ExactEquals(persisted.Yaw, received.Yaw)
           && string.Equals(
               TelemetrySchema.NormalizeQuality(persisted.CalidadSensor),
               TelemetrySchema.NormalizeQuality(received.CalidadSensor),
               StringComparison.Ordinal)
           && string.Equals(
               persisted.SensorFlagsCsv,
               TelemetryCanonicalizer.NormalizeSensorFlags(received.SensorFlags),
               StringComparison.Ordinal);

    public static bool IsIdentical(ViajeTelemetry a, ViajeTelemetry b)
        => a.ViajeId == b.ViajeId
           && a.Timestamp == b.Timestamp
           && a.SequenceNumber == b.SequenceNumber
           && ExactEquals(a.Lat, b.Lat)
           && ExactEquals(a.Lng, b.Lng)
           && ExactEquals(a.Velocidad, b.Velocidad)
           && ExactEquals(a.Altitud, b.Altitud)
           && ExactEquals(a.Heading, b.Heading)
           && ExactEquals(a.GpsAccuracyMeters, b.GpsAccuracyMeters)
           && ExactEquals(a.AceleracionX, b.AceleracionX)
           && ExactEquals(a.AceleracionY, b.AceleracionY)
           && ExactEquals(a.AceleracionZ, b.AceleracionZ)
           && ExactEquals(a.MagnitudAceleracion, b.MagnitudAceleracion)
           && ExactEquals(a.GiroscopioX, b.GiroscopioX)
           && ExactEquals(a.GiroscopioY, b.GiroscopioY)
           && ExactEquals(a.GiroscopioZ, b.GiroscopioZ)
           && ExactEquals(a.MagnitudGiroscopio, b.MagnitudGiroscopio)
           && ExactEquals(a.Desaceleracion, b.Desaceleracion)
           && a.FrecuenciaCardiaca == b.FrecuenciaCardiaca
           && ExactEquals(a.HrvMilisegundos, b.HrvMilisegundos)
           && ExactEquals(a.Spo2Porcentaje, b.Spo2Porcentaje)
           && ExactEquals(a.Pitch, b.Pitch)
           && ExactEquals(a.Roll, b.Roll)
           && ExactEquals(a.Yaw, b.Yaw)
           && string.Equals(
               TelemetrySchema.NormalizeQuality(a.CalidadSensor),
               TelemetrySchema.NormalizeQuality(b.CalidadSensor),
               StringComparison.Ordinal)
           && string.Equals(a.SensorFlagsCsv, b.SensorFlagsCsv, StringComparison.Ordinal);

    private static bool ExactEquals(double left, double right)
        => left.Equals(right);

    private static bool ExactEquals(double? left, double? right)
        => Nullable.Equals(left, right);
}
