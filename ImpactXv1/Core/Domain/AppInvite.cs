namespace Prueba1.Core.Domain;

public class AppInvite
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UsuarioId { get; set; }
    public string Token { get; set; } = string.Empty;
    public string? SuggestedUsername { get; set; }
    public string? Relation { get; set; }
    public string Priority { get; set; } = "Secundario";
    public string Status { get; set; } = "Pendiente de registro";
    public string? PersonalMessage { get; set; }
    public bool AutoAddToNetwork { get; set; } = true;
    public string InviteUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
}
