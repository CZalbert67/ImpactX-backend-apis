namespace ImpactX.Core.Domain;

public class Incidente
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UsuarioId { get; set; }
    public Guid AlertaId { get; set; }
    public string Tipo { get; set; } = "Impacto";
    public string Severidad { get; set; } = string.Empty;
    public string Estado { get; set; } = "Enviada";
    public double Lat { get; set; }
    public double Lng { get; set; }
    public string? Lugar { get; set; }
    public string? GForce { get; set; }
    public string? Decibeles { get; set; }
    public string? FrecuenciaCardiaca { get; set; }
    public string? Canal { get; set; }
    public string? ViajeId { get; set; }
    public Guid? SourceTelemetryEventId { get; set; }
    public string? DetectionLabel { get; set; }
    public string? RuleVersion { get; set; }
    public int? DetectionScore { get; set; }
    public string MetodoCierre { get; set; } = string.Empty;
    public bool EsFalsaAlarma { get; set; }
    public bool EsBypassCritico { get; set; }
    public bool EsOffline { get; set; }
    public string? Nota { get; set; }
    public List<string[]> Timeline { get; set; } = [];
    public List<string> ContactosNotificados { get; set; } = [];
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;
    public DateTime? EnviadaEn { get; set; }
    public DateTime? ConfirmadaEn { get; set; }
    public DateTime? CerradaEn { get; set; }

    // El contenedor Incidentes mantiene TTL por documento. 365 días = 31,536,000 segundos.
    public int Ttl { get; set; } = 31_536_000;
}
