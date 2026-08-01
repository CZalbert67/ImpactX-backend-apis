namespace ImpactX.Core.Domain;

public class Wearable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UsuarioId { get; set; }
    public string DispositivoId { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Modelo { get; set; } = string.Empty;
    public DateTime VinculadoEn { get; set; } = DateTime.UtcNow;
    public DateTime? UltimaSincronizacion { get; set; }
    public string? AppVersion { get; set; }
    public bool Connected { get; set; }
    public int NivelBateria { get; set; }
    public bool Calibrado { get; set; }
    public int CalibracionPorcentaje { get; set; }
    public DateTime? UltimaCalibracion { get; set; }
    public List<string> PermisosOtorgados { get; set; } = [];
    public string? PairingToken { get; set; }
    public string? CodigoEmparejamiento { get; set; }
    public string? TrustToken { get; set; }
    public WearableSensores SensoresActivos { get; set; } = new();
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
