namespace ImpactX.Core.Telemetry;

/// <summary>
/// Límites y rangos del contrato de ingesta de telemetría por lotes.
/// El cuerpo HTTP está limitado a 32 KB (suficiente para 100 eventos).
/// </summary>
public static class TelemetryIngestionLimits
{
    /// <summary>Máximo de eventos por lote (límite de TransactionalBatch de Cosmos).</summary>
    public const int MaxEventsPerBatch = 100;

    /// <summary>Mínimo de eventos por lote.</summary>
    public const int MinEventsPerBatch = 1;

    /// <summary>Tamaño máximo del cuerpo HTTP de la petición (bytes).</summary>
    public const long MaxBodyBytes = 32 * 1024;

    /// <summary>Tolerancia máxima para timestamps futuros (reloj del cliente).</summary>
    public static readonly TimeSpan MaxFutureTolerance = TimeSpan.FromMinutes(5);

    /// <summary>Latitud válida en grados decimales (WGS84).</summary>
    public const double MinLatitude = -90;
    public const double MaxLatitude = 90;

    /// <summary>Longitud válida en grados decimales (WGS84).</summary>
    public const double MinLongitude = -180;
    public const double MaxLongitude = 180;

    /// <summary>Velocidad válida en km/h (vehículos de consumo).</summary>
    public const double MinSpeedKmh = 0;
    public const double MaxSpeedKmh = 500;

    /// <summary>Altitud válida en metros sobre el nivel del mar.</summary>
    public const double MinAltitudeMeters = -500;
    public const double MaxAltitudeMeters = 10000;

    /// <summary>Heading (rumbo) válido en grados [0, 360).</summary>
    public const double MinHeadingDegrees = 0;
    public const double MaxHeadingDegrees = 360;
}
