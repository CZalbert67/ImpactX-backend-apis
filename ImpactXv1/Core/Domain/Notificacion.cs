namespace ImpactX.Core.Domain;

public class Notificacion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UsuarioId { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
    public string Tipo { get; set; } = "Info";
    public string? ReferenciaId { get; set; }
    public string? ReferenciaTipo { get; set; }
    public bool Leida { get; set; }
    public DateTime? LeidaEn { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}
