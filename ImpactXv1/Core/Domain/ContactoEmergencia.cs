namespace ImpactX.Core.Domain;

public class ContactoEmergencia
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UsuarioId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string? Parentesco { get; set; }
    public string? Username { get; set; }
    public string? AppUserId { get; set; }
    public string Channel { get; set; } = "Chat interno";
    public Guid? MonitorId { get; set; }
    public string Priority { get; set; } = "Secundario";
    public bool EsPrincipal { get; set; }
    public string? Email { get; set; }
    public string Status { get; set; } = "Activo";
    public string? Notes { get; set; }
    public string? PreviousStatus { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}
