using ImpactX.Core.Domain;
using ImpactX.Core.Pagination;

namespace ImpactX.Core.Interfaces.Repositories;

public interface IAlertaRepository
{
    Task<Alerta?> GetByIdAsync(Guid id);
    Task<Alerta?> GetByIdAsync(Guid usuarioId, Guid id);
    Task<List<Alerta>> GetByUserAsync(Guid usuarioId);
    Task<PagedResult<Alerta>> GetByUserPagedAsync(Guid usuarioId, int pageSize, string? continuationToken, CancellationToken cancellationToken = default);
    Task<Alerta?> GetActiveByUserAsync(Guid usuarioId);
    Task<List<Alerta>> GetPendingByUserAsync(Guid usuarioId);
    Task<List<Alerta>> GetActiveAlertsAsync(Guid usuarioId);
    Task AddAsync(Alerta alerta);
    Task UpdateAsync(Alerta alerta);
    Task<int> CountByUserAsync(Guid usuarioId);
}
