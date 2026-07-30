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
        var maskedCorreo = MaskEmail(correo);
        _logger.LogInformation(
            "[StubEmailService] Password reset requested for {Correo}. Email sending not yet implemented. Token was generated and stored internally.",
            maskedCorreo);
        return Task.CompletedTask;
    }

    private static string MaskEmail(string email)
    {
        var atIndex = email.IndexOf('@');
        if (atIndex <= 1) return email;
        return email[..1] + "***" + email[atIndex..];
    }
}
