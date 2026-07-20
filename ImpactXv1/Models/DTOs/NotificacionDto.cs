namespace ImpactX.Models.DTOs;

public class NotificacionDto
{
    public Guid Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string? ReferenciaId { get; set; }
    public string? ReferenciaTipo { get; set; }
    public bool Leida { get; set; }
    public DateTime? LeidaEn { get; set; }
    public DateTime CreadoEn { get; set; }
}

public class ToggleReadRequest
{
    public bool Leida { get; set; }
}
