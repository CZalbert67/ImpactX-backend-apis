namespace Prueba1.Core.Interfaces.Services;

public interface IEmailService
{
    Task SendPasswordResetEmailAsync(string correo, string token);
}
