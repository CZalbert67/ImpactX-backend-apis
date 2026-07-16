namespace Prueba1.Models.DTOs;

public class UserProfileDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string AppId { get; set; } = string.Empty;
    public string InviteCode { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string? PlanActivo { get; set; }
    public bool EmailConfirmed { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DriverProfileDto? PerfilConduccion { get; set; }
    public MedicalProfileDto? FichaMedica { get; set; }
    public UserPreferencesDto? Preferencias { get; set; }
    public PermisosDto? Permisos { get; set; }
    public SettingsDto? Settings { get; set; }
}

public class UpdateUserProfileRequest
{
    public string? Nombre { get; set; }
    public string? Telefono { get; set; }
}

public class UserPreferencesDto
{
    public bool NotificacionesPush { get; set; }
    public bool NotificacionesEmail { get; set; }
    public bool CompartirUbicacion { get; set; }
    public string? Idioma { get; set; }
    public string? UnidadVelocidad { get; set; }
}

public class UpdateUserPreferencesRequest
{
    public bool? NotificacionesPush { get; set; }
    public bool? NotificacionesEmail { get; set; }
    public bool? CompartirUbicacion { get; set; }
    public string? Idioma { get; set; }
    public string? UnidadVelocidad { get; set; }
}

public class DriverProfileDto
{
    public string? TipoVehiculo { get; set; }
    public string? Marca { get; set; }
    public string? Modelo { get; set; }
    public int? Anio { get; set; }
    public string? Color { get; set; }
    public string? Placa { get; set; }
    public string? Uso { get; set; }
    public string? VelocidadPromedioLabel { get; set; }
}

public class UpdateDriverProfileRequest
{
    public string? TipoVehiculo { get; set; }
    public string? Marca { get; set; }
    public string? Modelo { get; set; }
    public int? Anio { get; set; }
    public string? Color { get; set; }
    public string? Placa { get; set; }
    public string? Uso { get; set; }
    public string? VelocidadPromedioLabel { get; set; }
}

public class MedicalProfileDto
{
    public string? TipoSangre { get; set; }
    public string? Alergias { get; set; }
    public string? Condiciones { get; set; }
    public string? Medicamentos { get; set; }
    public string? Nota { get; set; }
}

public class UpdateMedicalProfileRequest
{
    public string? TipoSangre { get; set; }
    public string? Alergias { get; set; }
    public string? Condiciones { get; set; }
    public string? Medicamentos { get; set; }
    public string? Nota { get; set; }
}

public class PermisosDto
{
    public PermisosPlataformaDto? Mobile { get; set; }
    public PermisosPlataformaDto? Web { get; set; }
}

public class PermisosPlataformaDto
{
    public bool Ubicacion { get; set; }
    public bool Notificaciones { get; set; }
    public bool Camara { get; set; }
    public bool Microfono { get; set; }
    public bool Sensores { get; set; }
    public bool Bluetooth { get; set; }
}

public class SettingsDto
{
    public bool TwoFactorEnabled { get; set; }
}

public class UserSearchResultDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string AppId { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Correo { get; set; }
}
