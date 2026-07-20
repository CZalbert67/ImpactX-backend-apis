namespace ImpactX.Core.Domain;

public class Alerta
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UsuarioId { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Severidad { get; set; } = "bump";
    public string Estado { get; set; } = "Pendiente";
    public double Lat { get; set; }
    public double Lng { get; set; }
    public string? Lugar { get; set; }
    public string? GForce { get; set; }
    public string? Decibeles { get; set; }
    public string? FrecuenciaCardiaca { get; set; }
    public string? Activacion { get; set; }
    public string Modo { get; set; } = "auto";
    public string? Canal { get; set; }
    public string? ViajeId { get; set; }
    public string? TiempoRespuesta { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public DateTime? EnviadaEn { get; set; }
    public DateTime? ConfirmadaEn { get; set; }
    public DateTime? CerradaEn { get; set; }
    public string? MetodoCierre { get; set; }
    public bool EsBypassCritico { get; set; }
    public bool EsOffline { get; set; }
    public bool EsFalsaAlarma { get; set; }
    public int? Reintentos { get; set; }
    public string? Nota { get; set; }
    public List<string[]> Timeline { get; set; } = [];
    public List<string> ContactosNotificados { get; set; } = [];
}
