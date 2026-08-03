using ImpactX.Core.Domain;
using ImpactX.Core.Pagination;

namespace ImpactX.Core.Interfaces.Repositories;

public interface ISuscripcionRepository
{
    Task<Suscripcion?> GetActiveByUserAsync(Guid usuarioId);
    Task<Suscripcion?> GetCurrentByUserAsync(Guid usuarioId);
    Task<List<Suscripcion>> GetHistoryByUserAsync(Guid usuarioId);
    Task<PagedResult<Suscripcion>> GetHistoryByUserPagedAsync(Guid usuarioId, int pageSize, string? continuationToken, CancellationToken cancellationToken = default);
    Task<Suscripcion?> GetByIdAsync(Guid id);
    Task<Suscripcion?> GetByIdAsync(Guid usuarioId, Guid id);
    Task AddAsync(Suscripcion suscripcion);
    Task UpdateAsync(Suscripcion suscripcion);
    Task<List<Suscripcion>> GetExpiredAsync();
    Task<List<Suscripcion>> GetTrialsEndingAsync(int daysRemaining);
    Task<int> ExpireAllAsync(Func<Suscripcion, CancellationToken, Task> process, CancellationToken cancellationToken = default);
    Task<int> ProcessLifecycleAsync(DateTime utcNow, Func<Suscripcion, CancellationToken, Task> process, CancellationToken cancellationToken = default);
    Task<int> ProcessTrialsEndingAsync(int daysRemaining, Func<Suscripcion, CancellationToken, Task> process, CancellationToken cancellationToken = default);
}
