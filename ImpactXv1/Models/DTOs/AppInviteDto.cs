namespace ImpactX.Models.DTOs;

public class AppInviteDto
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public string Token { get; set; } = string.Empty;
    public string? SuggestedUsername { get; set; }
    public string? Relation { get; set; }
    public string Priority { get; set; } = "Secundario";
    public string Status { get; set; } = string.Empty;
    public string? PersonalMessage { get; set; }
    public bool AutoAddToNetwork { get; set; } = true;
    public string InviteUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool Expirada => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;
}

public class CreateAppInviteRequest
{
    public string? SuggestedUsername { get; set; }
    public string? Relation { get; set; }
    public string Priority { get; set; } = "Secundario";
    public string? PersonalMessage { get; set; }
    public bool AutoAddToNetwork { get; set; } = true;
}

public class AcceptAppInviteRequest
{
    public string Token { get; set; } = string.Empty;
}
