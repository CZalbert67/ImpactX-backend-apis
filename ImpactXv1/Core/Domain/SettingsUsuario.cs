namespace Prueba1.Core.Domain;

public class SettingsUsuario
{
    public bool TwoFactorEnabled { get; set; }
    public string? TwoFactorSecret { get; set; }
    public DateTime? TwoFactorVerifiedAt { get; set; }
}
