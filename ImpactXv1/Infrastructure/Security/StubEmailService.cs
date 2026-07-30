using ImpactX.Core.Interfaces.Services;

namespace ImpactX.Infrastructure.Security;

public class StubEmailService : IEmailService
{
    private readonly ILogger<StubEmailService> _logger;

    public StubEmailService(ILogger<StubEmailService> logger)
    {
        _logger = logger;
    }

    public Task SendPasswordResetEmailAsync(string correo, string token)
    {
        _logger.LogInformation(
            "[StubEmailService] Password reset delivery requested. Email sending is not configured.");
        return Task.CompletedTask;
    }
}
