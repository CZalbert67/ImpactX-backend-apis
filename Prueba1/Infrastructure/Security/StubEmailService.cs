using Prueba1.Core.Interfaces.Services;

namespace Prueba1.Infrastructure.Security;

public class StubEmailService : IEmailService
{
    private readonly ILogger<StubEmailService> _logger;

    public StubEmailService(ILogger<StubEmailService> logger)
    {
        _logger = logger;
    }

    public Task SendPasswordResetEmailAsync(string correo, string token)
    {
        _logger.LogInformation("Password reset email for {Correo}: token = {Token}", correo, token);
        return Task.CompletedTask;
    }
}
