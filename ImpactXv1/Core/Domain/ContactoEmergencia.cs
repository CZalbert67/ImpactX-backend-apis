using System.Text.Json.Serialization;
using ImpactX.Core.Domain.Enums;

namespace ImpactX.Core.Domain;

public class ContactoEmergencia
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    // Propietario de la relación y partition key del contenedor existente.
    [JsonPropertyName("usuarioId")]
    public Guid UsuarioId { get; set; }

    // Contrato V1: identificador público seguro. Los documentos históricos
    // carecen de este campo y se tratan como LegacyUnverified.
    [JsonPropertyName("publicContactId")]
    public string? PublicContactId { get; set; }

    [JsonPropertyName("contactUserId")]
    public Guid? ContactUserId { get; set; }

    [JsonPropertyName("targetEmailNormalized")]
    public string? TargetEmailNormalized { get; set; }

    [JsonPropertyName("targetPublicProfileId")]
    public string? TargetPublicProfileId { get; set; }

    [JsonPropertyName("targetUsername")]
    public string? TargetUsername { get; set; }

    [JsonPropertyName("invitationCodeHash")]
    public string? InvitationCodeHash { get; set; }

    [JsonPropertyName("status")]
    public EmergencyContactStatus Status { get; set; } = EmergencyContactStatus.LegacyUnverified;

    [JsonPropertyName("requestedAtUtc")]
    public DateTime? RequestedAtUtc { get; set; }

    [JsonPropertyName("expiresAtUtc")]
    public DateTime? ExpiresAtUtc { get; set; }

    [JsonPropertyName("requestedPrimary")]
    public bool RequestedPrimary { get; set; }

    [JsonPropertyName("acceptedAtUtc")]
    public DateTime? AcceptedAtUtc { get; set; }

    [JsonPropertyName("rejectedAtUtc")]
    public DateTime? RejectedAtUtc { get; set; }

    [JsonPropertyName("revokedAtUtc")]
    public DateTime? RevokedAtUtc { get; set; }

    [JsonPropertyName("blockedAtUtc")]
    public DateTime? BlockedAtUtc { get; set; }

    [JsonPropertyName("updatedAtUtc")]
    public DateTime? UpdatedAtUtc { get; set; }

    // Campos legacy conservados para lectura de documentos y ruta /api/contacts.
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
    public string? Notes { get; set; }
    public string? PreviousStatus { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

    [Newtonsoft.Json.JsonProperty("_etag")]
    [JsonPropertyName("_etag")]
    public string? ETag { get; set; }
}
