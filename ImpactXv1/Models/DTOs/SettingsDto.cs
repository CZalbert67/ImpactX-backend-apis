namespace ImpactX.Models.DTOs;

public class SettingsResponseDto
{
    public string? Idioma { get; set; }
    public string? UnidadVelocidad { get; set; }
    public bool NotificacionesPush { get; set; }
    public bool NotificacionesEmail { get; set; }
    public bool CompartirUbicacion { get; set; }
    public bool TwoFactorEnabled { get; set; }
}

public class UpdateSettingsRequest
{
    public string? Idioma { get; set; }
    public string? UnidadVelocidad { get; set; }
    public bool? NotificacionesPush { get; set; }
    public bool? NotificacionesEmail { get; set; }
    public bool? CompartirUbicacion { get; set; }
}

public class Setup2FaResponse
{
    public string Secret { get; set; } = string.Empty;
    public string QrCodeUri { get; set; } = string.Empty;
    public string ManualKey { get; set; } = string.Empty;
}

public class Enable2FaRequest
{
    public string Code { get; set; } = string.Empty;
}

public class Disable2FaRequest
{
    public string Code { get; set; } = string.Empty;
}
