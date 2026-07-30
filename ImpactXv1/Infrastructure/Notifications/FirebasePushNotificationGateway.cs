using ImpactX.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace ImpactX.Infrastructure.Notifications;

public class FirebasePushNotificationGateway : IPushNotificationGateway
{
    private readonly ILogger<FirebasePushNotificationGateway> _logger;

    public FirebasePushNotificationGateway(ILogger<FirebasePushNotificationGateway> logger)
    {
        _logger = logger;
    }

    public async Task<PushGatewayResult> SendAsync(
        string deviceToken,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default)
    {
        if (FirebaseAdmin.FirebaseApp.DefaultInstance == null)
        {
            _logger.LogWarning("FirebaseApp no inicializado. Push omitido.");
            return new PushGatewayResult(false, "FirebaseNoConfigurado");
        }

        try
        {
            var message = new FirebaseAdmin.Messaging.Message
            {
                Token = deviceToken,
                Notification = new FirebaseAdmin.Messaging.Notification
                {
                    Title = title,
                    Body = body,
                },
                Data = data?.ToDictionary(kv => kv.Key, kv => kv.Value),
            };

            var response = await FirebaseAdmin.Messaging.FirebaseMessaging.DefaultInstance.SendAsync(message, cancellationToken);

            _logger.LogInformation("Notificación push enviada con éxito.");
            return new PushGatewayResult(true, "Enviado", response);
        }
        catch (FirebaseAdmin.Messaging.FirebaseMessagingException)
        {
            _logger.LogWarning("Error categorizado de Firebase al enviar notificación push.");
            return new PushGatewayResult(false, "Fallido");
        }
        catch (Exception)
        {
            _logger.LogWarning("Error inesperado de tipo {ExceptionType} en gateway push.", nameof(Exception));
            return new PushGatewayResult(false, "Fallido");
        }
    }
}
