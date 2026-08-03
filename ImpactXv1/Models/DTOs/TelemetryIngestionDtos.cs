using System.Text.Json.Serialization;
using ImpactX.Converters;

namespace ImpactX.Models.DTOs;

/// <summary>
/// Lote de eventos de telemetría enviado por el wearable. La versión 1
/// conserva el contrato mínimo histórico. La versión 2 agrega metadatos de
/// sincronización offline, procedencia y sensores enriquecidos.
/// </summary>
public class TelemetryBatchRequest
{
    public int SchemaVersion { get; set; } = 1;
    public Guid? BatchId { get; set; }
    public long? BatchSequence { get; set; }
    public bool CapturedOffline { get; set; }

    public string? WearableDeviceId { get; set; }
    public string? WearableModel { get; set; }
    public string? WearableAppVersion { get; set; }
    public string? WearableOsVersion { get; set; }
    public string? WearableFirmwareVersion { get; set; }
    public int? BatteryLevel { get; set; }
    public long? ClockOffsetMilliseconds { get; set; }

    public List<TelemetryEventRequest> Eventos { get; set; } = [];
}

/// <summary>
/// Evento individual de telemetría. EventId es generado por el wearable y se
/// convierte en el identificador persistido del documento. Reenviar el mismo
/// EventId con el mismo contenido es seguro y no crea duplicados.
/// </summary>
public class TelemetryEventRequest
{
    public Guid EventId { get; set; }

    /// <summary>Cuándo ocurrió el evento según el wearable. Debe ser UTC explícito.</summary>
    [JsonConverter(typeof(UtcTimestampJsonConverter))]
    public DateTime Timestamp { get; set; }

    public long? SequenceNumber { get; set; }

    public double Lat { get; set; }
    public double Lng { get; set; }
    public double Velocidad { get; set; }
    public double? Altitud { get; set; }
    public double? Heading { get; set; }
    public double? GpsAccuracyMeters { get; set; }

    /// <summary>Aceleración lineal en m/s².</summary>
    public double? AceleracionX { get; set; }
    public double? AceleracionY { get; set; }
    public double? AceleracionZ { get; set; }
    public double? MagnitudAceleracion { get; set; }

    /// <summary>Velocidad angular en rad/s.</summary>
    public double? GiroscopioX { get; set; }
    public double? GiroscopioY { get; set; }
    public double? GiroscopioZ { get; set; }
    public double? MagnitudGiroscopio { get; set; }

    /// <summary>Desaceleración positiva estimada en m/s².</summary>
    public double? Desaceleracion { get; set; }

    public int? FrecuenciaCardiaca { get; set; }
    public double? HrvMilisegundos { get; set; }
    public double? Spo2Porcentaje { get; set; }

    /// <summary>Orientación en grados.</summary>
    public double? Pitch { get; set; }
    public double? Roll { get; set; }
    public double? Yaw { get; set; }

    /// <summary>unknown, low, medium o high.</summary>
    public string? CalidadSensor { get; set; }

    /// <summary>
    /// Banderas técnicas sin datos personales, por ejemplo gps_degraded,
    /// heart_rate_unavailable o sensor_saturated.
    /// </summary>
    public List<string> SensorFlags { get; set; } = [];
}

/// <summary>
/// Resultado de la ingesta de un lote. Solo contiene conteos y metadatos de
/// sincronización; nunca expone documentos internos ni EventIds.
/// </summary>
public class TelemetryIngestionResultDto
{
    public Guid ViajeId { get; set; }
    public Guid? BatchId { get; set; }
    public int SchemaVersion { get; set; }
    public bool CapturedOffline { get; set; }
    public int Recibidos { get; set; }
    public int Insertados { get; set; }
    public int Duplicados { get; set; }
    public DateTime PrimerEventoUtc { get; set; }
    public DateTime UltimoEventoUtc { get; set; }
    public long? PrimeraSecuencia { get; set; }
    public long? UltimaSecuencia { get; set; }
    public DateTime ProcesadoEnUtc { get; set; }
}
