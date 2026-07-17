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
    public DateTime? UltimaCalibracion { get; set; }
    public List<string> PermisosOtorgados { get; set; } = [];
    public string? PairingToken { get; set; }
    public string Estado { get; set; } = "Pendiente";
}
