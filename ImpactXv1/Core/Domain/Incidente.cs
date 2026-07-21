namespace ImpactX.Core.Domain;

public class Incidente
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UsuarioId { get; set; }
    public Guid AlertaId { get; set; }
    public string Severidad { get; set; } = string.Empty;
    public double Lat { get; set; }
    public double Lng { get; set; }
    public string? Lugar { get; set; }
    public string? GForce { get; set; }
    public string? Decibeles { get; set; }
    public string? FrecuenciaCardiaca { get; set; }
    public string? Canal { get; set; }
    public string MetodoCierre { get; set; } = string.Empty;
    public bool EsFalsaAlarma { get; set; }
    public bool EsBypassCritico { get; set; }
    public bool EsOffline { get; set; }
    public string? Nota { get; set; }
    public List<string[]> Timeline { get; set; } = [];
    public List<string> ContactosNotificados { get; set; } = [];
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public DateTime? CerradaEn { get; set; }
}
