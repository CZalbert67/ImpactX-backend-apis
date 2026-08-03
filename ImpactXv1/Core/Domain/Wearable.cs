using ImpactX.Core.Wearables;

namespace ImpactX.Core.Domain;

public class Wearable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UsuarioId { get; set; }
    public string DispositivoId { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Modelo { get; set; } = string.Empty;
    public string Fabricante { get; set; } = WearableProductPolicy.TargetManufacturer;
    public string Plataforma { get; set; } = WearableProductPolicy.TargetPlatform;
    public DateTime VinculadoEn { get; set; } = DateTime.UtcNow;
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
    public List<string> CapacidadesSensores { get; set; } = [];
    public List<string> SensoresDisponibles { get; set; } = [];
    public List<string> SensoresNoDisponibles { get; set; } = [];
    public string? CalidadSensores { get; set; }
    /// <summary>Hash SHA-256 del código; documentos legacy pueden contener el código en claro.</summary>
    public string? PairingToken { get; set; }
    public string? CodigoEmparejamiento { get; set; }
    public string? TrustToken { get; set; }
    public WearableSensores SensoresActivos { get; set; } = new();
    public DateTime? PairingExpiresAtUtc { get; set; }
    public string Estado { get; set; } = "Pendiente";
}

public class WearableSensores
{
    public bool Acelerometro { get; set; }
    public bool Microfono { get; set; }
    public bool FrecuenciaCardiaca { get; set; }
    public bool Gps { get; set; }
    public bool SegundoPlano { get; set; }
}
