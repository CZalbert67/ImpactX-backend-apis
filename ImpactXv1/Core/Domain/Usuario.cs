namespace Prueba1.Core.Domain;

public class Usuario
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public string AppId { get; set; } = string.Empty;
    public string InviteCode { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public bool EmailConfirmed { get; set; }
    public string? PlanActivo { get; set; }

    public PerfilConduccion? PerfilConduccion { get; set; }
    public FichaMedica? FichaMedica { get; set; }
    public PreferenciasUsuario? Preferencias { get; set; }
    public PermisosApp? Permisos { get; set; }
    public SettingsUsuario? Settings { get; set; }
}
