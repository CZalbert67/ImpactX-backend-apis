namespace ImpactX.Models.DTOs;

public class MonitorDto
{
    public Guid Id { get; set; }
    public string? Nombre { get; set; }
    public string? Telefono { get; set; }
    public string? CorreoInvitado { get; set; }
    public string? Username { get; set; }
    public string? AppUserId { get; set; }
    public string? ProfileId { get; set; }
    public string Estado { get; set; } = string.Empty;
    public Guid? ContactoId { get; set; }
    public DateTime CreadoEn { get; set; }
    public DateTime? Expiracion { get; set; }
    public DateTime? ConfirmadoEn { get; set; }
    public DateTime? RevocadoEn { get; set; }
    public string? Token { get; set; }
    public List<string> Permisos { get; set; } = [];
}

public class InviteMonitorRequest
{
    public string? Nombre { get; set; }
    public string? Telefono { get; set; }
    public string? CorreoInvitado { get; set; }
    public string? Username { get; set; }
    public string? AppUserId { get; set; }
    public List<string>? Permisos { get; set; }
}

public class InviteMonitorResponse
{
    public Guid MonitorId { get; set; }
    public string Token { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
}

public class InvitationInfoDto
{
    // Compatibilidad: contiene el PublicProfileId del usuario que invita,
    // nunca la primary key interna ni identificadores de base de datos.
    public string Id { get; set; } = string.Empty;
    public string PublicProfileId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? Nombre { get; set; }
    public string? CorreoInvitado { get; set; }
    public string Estado { get; set; } = string.Empty;
    public DateTime CreadoEn { get; set; }
    public DateTime? Expiracion { get; set; }
    public bool Expirada => Expiracion.HasValue && DateTime.UtcNow > Expiracion.Value;
}
