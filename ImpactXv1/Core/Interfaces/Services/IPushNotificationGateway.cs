namespace ImpactX.Core.Interfaces.Services;

public sealed record PushGatewayResult(
    bool Success,
    string Status,
    string? ExternalMessageId = null);

public interface IPushNotificationGateway
{
    Task<PushGatewayResult> SendAsync(
        string deviceToken,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default);
}
