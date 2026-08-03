namespace ImpactX.Core.Telemetry;

/// <summary>
/// Límites y rangos del contrato de ingesta de telemetría por lotes.
/// El cuerpo HTTP admite hasta 256 KiB para 100 eventos enriquecidos.
/// </summary>
public static class TelemetryIngestionLimits
{
    public const int MaxEventsPerBatch = 100;
    public const int MinEventsPerBatch = 1;
    public const long MaxBodyBytes = 256 * 1024;
    public static readonly TimeSpan MaxFutureTolerance = TimeSpan.FromMinutes(5);

    public const double MinLatitude = -90;
    public const double MaxLatitude = 90;
    public const double MinLongitude = -180;
    public const double MaxLongitude = 180;
    public const double MinSpeedKmh = 0;
    public const double MaxSpeedKmh = 500;
    public const double MinAltitudeMeters = -500;
    public const double MaxAltitudeMeters = 10000;
    public const double MinHeadingDegrees = 0;
    public const double MaxHeadingDegrees = 360;

    public const double MinGpsAccuracyMeters = 0;
    public const double MaxGpsAccuracyMeters = 5000;

    public const double MinAccelerationMps2 = -250;
    public const double MaxAccelerationMps2 = 250;
    public const double MinAccelerationMagnitudeMps2 = 0;
    public const double MaxAccelerationMagnitudeMps2 = 450;

    public const double MinGyroscopeRadPerSecond = -50;
    public const double MaxGyroscopeRadPerSecond = 50;
    public const double MinGyroscopeMagnitudeRadPerSecond = 0;
    public const double MaxGyroscopeMagnitudeRadPerSecond = 90;

    public const double MinDecelerationMps2 = 0;
    public const double MaxDecelerationMps2 = 150;

    public const int MinHeartRateBpm = 20;
    public const int MaxHeartRateBpm = 250;
    public const double MinHrvMilliseconds = 0;
    public const double MaxHrvMilliseconds = 500;
    public const double MinSpo2Percent = 50;
    public const double MaxSpo2Percent = 100;

    public const double MinPitchDegrees = -180;
    public const double MaxPitchDegrees = 180;
    public const double MinRollDegrees = -180;
    public const double MaxRollDegrees = 180;
    public const double MinYawDegrees = 0;
    public const double MaxYawDegrees = 360;

    public const int MaxSensorFlags = 16;
    public const int MaxSensorFlagLength = 64;
    public const int MaxDeviceMetadataLength = 200;
    public const int MaxVersionLength = 80;

    public const long MinClockOffsetMilliseconds = -86_400_000;
    public const long MaxClockOffsetMilliseconds = 86_400_000;
}
