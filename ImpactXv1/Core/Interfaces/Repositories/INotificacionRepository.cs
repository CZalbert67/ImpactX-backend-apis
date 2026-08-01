using ImpactX.Core.Domain;
using ImpactX.Core.Pagination;

namespace ImpactX.Core.Interfaces.Repositories;

public interface INotificacionRepository
{
    Task<List<Notificacion>> GetByUserAsync(Guid usuarioId);
    Task<PagedResult<Notificacion>> GetByUserPagedAsync(Guid usuarioId, int pageSize, string? continuationToken, CancellationToken cancellationToken = default);
    Task<Notificacion?> GetByIdAsync(Guid id);
    Task<Notificacion?> GetByIdAsync(Guid usuarioId, Guid id);
    Task<int> CountUnreadByUserAsync(Guid usuarioId);
    Task<Notificacion?> GetByIdempotencyKeyAsync(string key, Guid? recipientUserId = null, CancellationToken cancellationToken = default);
    Task AddAsync(Notificacion notificacion);
    Task UpdateAsync(Notificacion notificacion);
    Task MarkAllAsReadAsync(Guid usuarioId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Notificacion notificacion);
    Task DeleteAllByUserAsync(Guid usuarioId, CancellationToken cancellationToken = default);
}
