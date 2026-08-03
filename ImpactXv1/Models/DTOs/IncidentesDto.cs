namespace ImpactX.Models.DTOs;

public class IncidenteListItemDto
{
    public Guid Id { get; set; }
    public Guid AlertaId { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Severidad { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public double Lat { get; set; }
    public double Lng { get; set; }
    public string? Lugar { get; set; }
    public string MetodoCierre { get; set; } = string.Empty;
    public bool EsFalsaAlarma { get; set; }
    public DateTime CreadoEn { get; set; }
    public DateTime ActualizadoEn { get; set; }
    public DateTime? CerradaEn { get; set; }
}

public class IncidenteDetailDto
{
    public Guid Id { get; set; }
    public Guid AlertaId { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Severidad { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public string? Fecha { get; set; }
    public string? Hora { get; set; }
    public double Lat { get; set; }
    public double Lng { get; set; }
    public string? Coords { get; set; }
    public string? Lugar { get; set; }
    public string? GForce { get; set; }
    public string? Decibeles { get; set; }
    public string? FrecuenciaCardiaca { get; set; }
    public string? Canal { get; set; }
    public string? Activacion { get; set; }
    public string? TiempoRespuesta { get; set; }
    public bool EsAutomatico { get; set; }
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
    public DateTime CreadoEn { get; set; }
    public DateTime ActualizadoEn { get; set; }
    public DateTime? EnviadaEn { get; set; }
    public DateTime? ConfirmadaEn { get; set; }
    public DateTime? CerradaEn { get; set; }
}

public class MarkFalseAlarmRequest
{
    public string? Nota { get; set; }
}

public class NoteRequest
{
    public string Nota { get; set; } = string.Empty;
}

public sealed class IncidentCloseRequest
{
    public string MetodoCierre { get; set; } = "Atendido";
    public string? Nota { get; set; }
}

public sealed class IncidentActionResponse
{
    public Guid IncidentId { get; set; }
    public Guid AlertId { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
}

public class MapDataDto
{
    public double Lat { get; set; }
    public double Lng { get; set; }
    public string? Lugar { get; set; }
    public string MapsUrl { get; set; } = string.Empty;
}

public class IncidentFilterRequest
{
    public string? Severidad { get; set; }
    public string? Estado { get; set; }
    public DateTime? Desde { get; set; }
    public DateTime? Hasta { get; set; }
    public int Pagina { get; set; } = 1;
    public int Tamano { get; set; } = 20;
}
