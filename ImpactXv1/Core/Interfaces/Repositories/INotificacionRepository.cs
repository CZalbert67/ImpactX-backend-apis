using ImpactX.Core.Domain;

namespace ImpactX.Core.Interfaces.Repositories;

public interface INotificacionRepository
{
    Task<List<Notificacion>> GetByUserAsync(Guid usuarioId);
    Task<Notificacion?> GetByIdAsync(Guid id);
    Task<int> CountUnreadByUserAsync(Guid usuarioId);
    Task AddAsync(Notificacion notificacion);
    Task UpdateAsync(Notificacion notificacion);
    Task MarkAllAsReadAsync(Guid usuarioId);
    Task DeleteAsync(Notificacion notificacion);
    Task DeleteAllByUserAsync(Guid usuarioId);
}
