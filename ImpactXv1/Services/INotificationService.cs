using ImpactX.Models.DTOs;

namespace ImpactX.Services;

public interface INotificationService
{
    Task<List<NotificacionDto>> GetNotificationsAsync(Guid usuarioId);
    Task<int> GetUnreadCountAsync(Guid usuarioId);
    Task ToggleReadAsync(Guid usuarioId, Guid notificacionId, ToggleReadRequest request);
    Task MarkAllAsReadAsync(Guid usuarioId);
    Task DeleteAsync(Guid usuarioId, Guid notificacionId);
    Task DeleteAllAsync(Guid usuarioId);
}
