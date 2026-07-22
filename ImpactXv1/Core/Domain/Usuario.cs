using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace ImpactX.Core.Domain;

public class Usuario
{
    [JsonProperty("id")]
    [JsonPropertyName("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [JsonProperty("username")]
    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonProperty("appId")]
    [JsonPropertyName("appId")]
    public string AppId { get; set; } = string.Empty;

    [JsonProperty("inviteCode")]
    [JsonPropertyName("inviteCode")]
    public string InviteCode { get; set; } = string.Empty;

    [JsonProperty("nombre")]
    [JsonPropertyName("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [JsonProperty("correo")]
    [JsonPropertyName("correo")]
    public string Correo { get; set; } = string.Empty;

    [JsonProperty("telefono")]
    [JsonPropertyName("telefono")]
    public string Telefono { get; set; } = string.Empty;

    [JsonProperty("passwordHash")]
    [JsonPropertyName("passwordHash")]
    public string PasswordHash { get; set; } = string.Empty;

    [JsonProperty("isActive")]
    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; } = true;

    [JsonProperty("createdAt")]
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonProperty("lastLoginAt")]
    [JsonPropertyName("lastLoginAt")]
    public DateTime? LastLoginAt { get; set; }

    [JsonProperty("emailConfirmed")]
    [JsonPropertyName("emailConfirmed")]
    public bool EmailConfirmed { get; set; }

    [JsonProperty("planActivo")]
    [JsonPropertyName("planActivo")]
    public string? PlanActivo { get; set; }

    [JsonProperty("perfilConduccion")]
    [JsonPropertyName("perfilConduccion")]
    public PerfilConduccion? PerfilConduccion { get; set; }

    [JsonProperty("fichaMedica")]
    [JsonPropertyName("fichaMedica")]
    public FichaMedica? FichaMedica { get; set; }

    [JsonProperty("preferencias")]
    [JsonPropertyName("preferencias")]
    public PreferenciasUsuario? Preferencias { get; set; }

    [JsonProperty("permisos")]
    [JsonPropertyName("permisos")]
    public PermisosApp? Permisos { get; set; }

    [JsonProperty("settings")]
    [JsonPropertyName("settings")]
    public SettingsUsuario? Settings { get; set; }
}
