namespace ImpactX.Models.DTOs;

public class WearableDto
{
    public Guid Id { get; set; }
    public string DispositivoId { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Modelo { get; set; } = string.Empty;
    public DateTime VinculadoEn { get; set; }
    public DateTime? UltimaSincronizacion { get; set; }
    public string? AppVersion { get; set; }
    public bool Connected { get; set; }
    public int NivelBateria { get; set; }
    public bool Calibrado { get; set; }
    public DateTime? UltimaCalibracion { get; set; }
    public List<string> PermisosOtorgados { get; set; } = [];
    public string Estado { get; set; } = string.Empty;
}

public class PairWearableRequest
{
    public string DispositivoId { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Modelo { get; set; } = string.Empty;
}

public class PairConfirmRequest
{
    public string Token { get; set; } = string.Empty;
}

public class PairResponse
{
    public string Token { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
}

public class SyncTelemetryRequest
{
    public List<TelemetryPointDto> Puntos { get; set; } = [];
}

public class TelemetryPointDto
{
    public double Lat { get; set; }
    public double Lng { get; set; }
    public double Velocidad { get; set; }
    public double? Altitud { get; set; }
    public double? Heading { get; set; }
    public DateTime Timestamp { get; set; }
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
}

public class SensorDiagnosticsDto
{
    public bool Acelerometro { get; set; }
    public bool Giroscopio { get; set; }
    public bool Magnetometro { get; set; }
    public bool Gps { get; set; }
    public bool FrecuenciaCardiaca { get; set; }
    public int NivelBateria { get; set; }
    public DateTime UltimoDiagnostico { get; set; }
}
