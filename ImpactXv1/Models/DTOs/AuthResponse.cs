namespace ImpactX.Models.DTOs;

public class AuthResponse
{
    public bool Success { get; set; }
    public string? Token { get; set; }
    public string? RefreshToken { get; set; }
    public string? ResetToken { get; set; }
    public string? Mensaje { get; set; }
    public UsuarioDto? Usuario { get; set; }
}

public class UsuarioDto
{
    // Compatibilidad: contiene PublicProfileId, nunca la primary key interna.
    public string Id { get; set; } = string.Empty;
    public string PublicProfileId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string? PlanActivo { get; set; }
}
