namespace ImpactX.Core.Interfaces.Services;

public interface IEmailService
{
    Task SendPasswordResetEmailAsync(string correo, string token);
}
