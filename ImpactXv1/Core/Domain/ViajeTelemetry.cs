namespace ImpactX.Core.Domain;

public class ViajeTelemetry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ViajeId { get; set; }
    public Guid UsuarioId { get; set; }
    public DateTime Timestamp { get; set; }
    public double Lat { get; set; }
    public double Lng { get; set; }
    public double Velocidad { get; set; }
    public double? Altitud { get; set; }
    public double? Heading { get; set; }

    // Contrato v2 / sincronización offline.
    public int SchemaVersion { get; set; } = 1;
    public Guid? BatchId { get; set; }
    public long? BatchSequence { get; set; }
    public long? SequenceNumber { get; set; }
    public bool CapturedOffline { get; set; }

    // Procedencia del wearable. Se desnormaliza deliberadamente para que el
    // futuro dataset ML pueda auditar firmware, app y dispositivo sin joins.
    public string? WearableDeviceId { get; set; }
    public string? WearableModel { get; set; }
    public string? WearableAppVersion { get; set; }
    public string? WearableOsVersion { get; set; }
    public string? WearableFirmwareVersion { get; set; }
    public string? VehiclePublicId { get; set; }
    public int? BatteryLevel { get; set; }
    public long? ClockOffsetMilliseconds { get; set; }

    // Calidad GPS y movimiento.
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

    // Biometría y orientación.
    public int? FrecuenciaCardiaca { get; set; }
    public double? HrvMilisegundos { get; set; }
    public double? Spo2Porcentaje { get; set; }
    public double? Pitch { get; set; }
    public double? Roll { get; set; }
    public double? Yaw { get; set; }
    public string? CalidadSensor { get; set; }
    public string? SensorFlagsCsv { get; set; }

    // Campos reservados para anotación del motor de reglas/ML. No se aceptan
    // desde el cliente en el contrato de ingesta.
    public bool? ImpactCandidate { get; set; }
    public string? DetectionLabel { get; set; }
    public string? SeverityLabel { get; set; }
    public string? RuleVersion { get; set; }
    public int? DetectionScore { get; set; }
    public string? ModelVersion { get; set; }
    public DateTime? LabeledAtUtc { get; set; }

    /// <summary>
    /// Fecha de recepción del evento en el servidor (UTC), separada
    /// claramente de <see cref="Timestamp"/> (cuándo ocurrió el evento según
    /// el cliente). Se excluye de la comparación idempotente.
    /// </summary>
    public DateTime RecibidoEn { get; set; } = DateTime.UtcNow;
}
