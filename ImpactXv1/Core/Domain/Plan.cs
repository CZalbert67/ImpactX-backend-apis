namespace ImpactX.Core.Domain;

public class Plan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Nombre { get; set; } = string.Empty;
    public decimal PrecioMensual { get; set; }
    public decimal PrecioAnual { get; set; }
    public int MaxContactos { get; set; }
    public int MaxMonitores { get; set; }
    public bool HistorialMapa { get; set; }
    public bool ExportacionDatos { get; set; }
    public bool SoportePrioritario { get; set; }
    public int DuracionTrialDias { get; set; } = 14;
}
