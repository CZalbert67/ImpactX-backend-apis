using System.Text.Json.Serialization;
using ImpactX.Converters;
using ImpactX.Core.Wearables;

namespace ImpactX.Models.DTOs;

public class WearableDto
{
    /// <summary>Identificador legacy. Se conserva mientras se introduce un id público.</summary>
    public Guid Id { get; set; }
    public string DispositivoId { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Modelo { get; set; } = string.Empty;
    public string Fabricante { get; set; } = WearableProductPolicy.TargetManufacturer;
    public string Plataforma { get; set; } = WearableProductPolicy.TargetPlatform;
    public DateTime VinculadoEn { get; set; }
    public DateTime? UltimaSincronizacion { get; set; }
    public DateTime? UltimoHeartbeatUtc { get; set; }
    public DateTime? UltimoDiagnosticoUtc { get; set; }
    public string? AppVersion { get; set; }
    public string? VersionSistemaOperativo { get; set; }
    public string? VersionFirmware { get; set; }
    public bool Connected { get; set; }
    public bool Cargando { get; set; }
    public int NivelBateria { get; set; }
    public long? DesfaseRelojMilisegundos { get; set; }
    public bool Calibrado { get; set; }
    public int CalibracionPorcentaje { get; set; }
    public DateTime? UltimaCalibracion { get; set; }
    public List<string> PermisosOtorgados { get; set; } = [];
    public string? CodigoEmparejamiento { get; set; }
    public string? TrustToken { get; set; }
    public WearableSensoresDto? SensoresActivos { get; set; }
    public List<string> CapacidadesSensores { get; set; } = [];
    public List<string> SensoresDisponibles { get; set; } = [];
    public List<string> SensoresNoDisponibles { get; set; } = [];
    public string? CalidadSensores { get; set; }
    public string Estado { get; set; } = string.Empty;
}

public class WearableSensoresDto
{
    public bool Acelerometro { get; set; }
    public bool Microfono { get; set; }
    public bool FrecuenciaCardiaca { get; set; }
    public bool Gps { get; set; }
    public bool SegundoPlano { get; set; }
}

public class PairWearableRequest
{
    public string DispositivoId { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Modelo { get; set; } = string.Empty;
    public string Fabricante { get; set; } = WearableProductPolicy.TargetManufacturer;
    public string Plataforma { get; set; } = WearableProductPolicy.TargetPlatform;
    public string? VersionSistemaOperativo { get; set; }
    public string? VersionFirmware { get; set; }
    public string? AppVersion { get; set; }
    public List<string> CapacidadesSensores { get; set; } = [];
}

public class PairConfirmRequest
{
    public string Token { get; set; } = string.Empty;
}

public class PairResponse
{
    public string Token { get; set; } = string.Empty;
    public string? CodigoEmparejamiento { get; set; }
    public string? TrustToken { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public string Mensaje { get; set; } = string.Empty;
}

/// <summary>
/// Endpoint legacy de sincronización de estado. La telemetría de viaje se
/// persiste exclusivamente mediante POST /api/v1/trips/{id}/telemetry.
/// </summary>
public class SyncTelemetryRequest
{
    public List<TelemetryPointDto> Puntos { get; set; } = [];
}

/// <summary>
/// DTO de lectura de telemetría. Conserva los campos históricos y agrega los
/// sensores del contrato v2. Los campos de anotación son de solo lectura.
/// </summary>
public class TelemetryPointDto
{
    public double Lat { get; set; }
    public double Lng { get; set; }
    public double Velocidad { get; set; }
    public double? Altitud { get; set; }
    public double? Heading { get; set; }
    public DateTime Timestamp { get; set; }

    public int SchemaVersion { get; set; } = 1;
    public long? SequenceNumber { get; set; }
    public bool CapturedOffline { get; set; }
    public string? WearableDeviceId { get; set; }
    public string? WearableModel { get; set; }
    public string? VehiclePublicId { get; set; }
    public int? BatteryLevel { get; set; }
    public double? GpsAccuracyMeters { get; set; }
    public double? AceleracionX { get; set; }
    public double? AceleracionY { get; set; }
    public double? AceleracionZ { get; set; }
    public double? MagnitudAceleracion { get; set; }
    public double? GiroscopioX { get; set; }
    public double? GiroscopioY { get; set; }
    public double? GiroscopioZ { get; set; }
    public double? MagnitudGiroscopio { get; set; }
    public double? Desaceleracion { get; set; }
    public int? FrecuenciaCardiaca { get; set; }
    public double? HrvMilisegundos { get; set; }
    public double? Spo2Porcentaje { get; set; }
    public double? Pitch { get; set; }
    public double? Roll { get; set; }
    public double? Yaw { get; set; }
    public string? CalidadSensor { get; set; }
    public List<string> SensorFlags { get; set; } = [];

    public bool? ImpactCandidate { get; set; }
    public string? DetectionLabel { get; set; }
    public string? SeverityLabel { get; set; }
    public string? RuleVersion { get; set; }
    public int? DetectionScore { get; set; }
    public string? ModelVersion { get; set; }
}

public class CalibrationRequest
{
    public bool Acelerometro { get; set; }
    public bool Giroscopio { get; set; }
    public bool Magnetometro { get; set; }
    public bool Gps { get; set; }
}

public class UpdateWearablePermissionsRequest
{
    public List<string> Permisos { get; set; } = [];
}

public class BatteryUpdateRequest
{
    public int Nivel { get; set; }
    public bool Cargando { get; set; }
}

public class WearableHeartbeatRequest
{
    public string DispositivoId { get; set; } = string.Empty;
    public string Modelo { get; set; } = WearableProductPolicy.TargetModel;
    public string Fabricante { get; set; } = WearableProductPolicy.TargetManufacturer;
    public string Plataforma { get; set; } = WearableProductPolicy.TargetPlatform;
    public string? AppVersion { get; set; }
    public string? VersionSistemaOperativo { get; set; }
    public string? VersionFirmware { get; set; }
    public int NivelBateria { get; set; }
    public bool Cargando { get; set; }
    public long? DesfaseRelojMilisegundos { get; set; }
    public List<string> CapacidadesSensores { get; set; } = [];

    [JsonConverter(typeof(UtcTimestampJsonConverter))]
    public DateTime TimestampUtc { get; set; }
}

public class WearableDiagnosticsReportRequest
{
    public string DispositivoId { get; set; } = string.Empty;
    public List<string> SensoresDisponibles { get; set; } = [];
    public List<string> SensoresNoDisponibles { get; set; } = [];
    public string CalidadGeneral { get; set; } = "unknown";

    [JsonConverter(typeof(UtcTimestampJsonConverter))]
    public DateTime TimestampUtc { get; set; }
}

public class SensorDiagnosticsDto
{
    public string Modelo { get; set; } = string.Empty;
    public string Fabricante { get; set; } = string.Empty;
    public string Plataforma { get; set; } = string.Empty;
    public bool Acelerometro { get; set; }
    public bool Giroscopio { get; set; }
    public bool Magnetometro { get; set; }
    public bool Gps { get; set; }
    public bool FrecuenciaCardiaca { get; set; }
    public bool Hrv { get; set; }
    public bool Spo2 { get; set; }
    public string CalidadGeneral { get; set; } = "unknown";
    public List<string> SensoresDisponibles { get; set; } = [];
    public List<string> SensoresNoDisponibles { get; set; } = [];
    public int NivelBateria { get; set; }
    public DateTime? UltimoDiagnosticoUtc { get; set; }
}
