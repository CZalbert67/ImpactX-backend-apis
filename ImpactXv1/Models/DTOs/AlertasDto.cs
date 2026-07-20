namespace ImpactX.Models.DTOs;

public class DetectAlertRequest
{
    public double Lat { get; set; }
    public double Lng { get; set; }
    public string? Lugar { get; set; }
    public double GForce { get; set; }
    public double Decibeles { get; set; }
    public double FrecuenciaCardiaca { get; set; }
    public string Severidad { get; set; } = "bump";
    public string? ViajeId { get; set; }
}

public class SosRequest
{
    public double Lat { get; set; }
    public double Lng { get; set; }
    public string? Lugar { get; set; }
    public string Severidad { get; set; } = "severe";
    public string Canal { get; set; } = "manual";
    public string? GForce { get; set; }
    public string? Decibeles { get; set; }
    public string? FrecuenciaCardiaca { get; set; }
    public string Modo { get; set; } = "manual";
    public string? ViajeId { get; set; }
}

public class CloseAlertRequest
{
    public string MetodoCierre { get; set; } = "Atendido";
    public string? Nota { get; set; }
}

public class SyncOfflineRequest
{
    public List<OfflineAlertDto> Alertas { get; set; } = [];
}

public class OfflineAlertDto
{
    public double Lat { get; set; }
    public double Lng { get; set; }
    public string? Lugar { get; set; }
    public string Severidad { get; set; } = "bump";
    public string Tipo { get; set; } = "SOS";
    public string? GForce { get; set; }
    public string? Decibeles { get; set; }
    public string? FrecuenciaCardiaca { get; set; }
    public DateTime CreadoEn { get; set; }
}

public class AlertStatusDto
{
    public Guid Id { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Severidad { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public double Lat { get; set; }
    public double Lng { get; set; }
    public string? Lugar { get; set; }
    public string? GForce { get; set; }
    public string? Decibeles { get; set; }
    public string? FrecuenciaCardiaca { get; set; }
    public string Modo { get; set; } = string.Empty;
    public string? Canal { get; set; }
    public string? ViajeId { get; set; }
    public bool EsBypassCritico { get; set; }
    public bool EsOffline { get; set; }
    public string? TiempoRespuesta { get; set; }
    public DateTime CreadoEn { get; set; }
    public DateTime? EnviadaEn { get; set; }
    public DateTime? ConfirmadaEn { get; set; }
    public DateTime? CerradaEn { get; set; }
    public string? MetodoCierre { get; set; }
    public string? Nota { get; set; }
    public List<string[]> Timeline { get; set; } = [];
    public List<string> ContactosNotificados { get; set; } = [];
}

public class AlertActionResponse
{
    public Guid AlertaId { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
}

public class ConfirmOkResponse
{
    public string Mensaje { get; set; } = string.Empty;
    public bool EsFalsaAlarma { get; set; }
}
