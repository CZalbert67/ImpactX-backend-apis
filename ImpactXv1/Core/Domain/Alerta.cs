namespace ImpactX.Core.Domain;

public class Alerta
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UsuarioId { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Estado { get; set; } = "Activa";
    public double Lat { get; set; }
    public double Lng { get; set; }
    public string? Lugar { get; set; }
    public string? GForce { get; set; }
    public string? Decibeles { get; set; }
    public string? FrecuenciaCardiaca { get; set; }
    public string? Activacion { get; set; }
    public string? TiempoRespuesta { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public DateTime? CerradaEn { get; set; }
    public string? MetodoCierre { get; set; }
    public string? Canal { get; set; }
    public bool EsBypassCritico { get; set; }
    public bool EsOffline { get; set; }
    public string? Nota { get; set; }
    public List<string[]> Timeline { get; set; } = [];
    public List<string> ContactosNotificados { get; set; } = [];
}
