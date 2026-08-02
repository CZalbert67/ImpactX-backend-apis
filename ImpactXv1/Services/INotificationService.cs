using ImpactX.Core.Domain;
using ImpactX.Core.Notifications;
using ImpactX.Core.Pagination;
using ImpactX.Models.DTOs;

namespace ImpactX.Services;

public interface INotificationService
{
    Task<List<NotificacionDto>> GetNotificationsAsync(Guid usuarioId);
    Task<PagedResult<NotificacionDto>> GetNotificationsPagedAsync(Guid usuarioId, int? pageSize, string? continuationToken);
    Task<int> GetUnreadCountAsync(Guid usuarioId);
    Task ToggleReadAsync(Guid usuarioId, Guid notificacionId, ToggleReadRequest request);
    Task MarkAllAsReadAsync(Guid usuarioId);
    Task DeleteAsync(Guid usuarioId, Guid notificacionId);
    Task DeleteAllAsync(Guid usuarioId);
    Task SendPushNotificationAsync(Guid usuarioId, string titulo, string mensaje, Dictionary<string, string>? datos = null);
    Task<IReadOnlyList<NotificationDispatchResult>> NotifyAlertMonitorsAsync(Alerta alerta, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NotificationDispatchResult>> RetryAlertNotificationsAsync(Alerta alerta, CancellationToken cancellationToken = default);
}
