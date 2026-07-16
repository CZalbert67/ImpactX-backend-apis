namespace Prueba1.Core.Domain;

public class Monitor
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UsuarioId { get; set; }
    public string? CorreoInvitado { get; set; }
    public string? Username { get; set; }
    public string? AppUserId { get; set; }
    public string? ProfileId { get; set; }
    public string Estado { get; set; } = "Pendiente";
    public string? TokenInvitacion { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public DateTime? Expiracion { get; set; }
    public DateTime? ConfirmadoEn { get; set; }
    public DateTime? RevocadoEn { get; set; }
    public List<string> Permisos { get; set; } = [];
}
