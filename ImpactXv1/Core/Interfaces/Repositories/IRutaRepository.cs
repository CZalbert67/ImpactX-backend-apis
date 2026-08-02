using ImpactX.Core.Domain;
using ImpactX.Core.Pagination;

namespace ImpactX.Core.Interfaces.Repositories;

public interface IRutaRepository
{
    Task<List<Ruta>> GetByUserAsync(Guid usuarioId);
    Task<List<Ruta>> GetFrequentByUserAsync(Guid usuarioId);
    Task<List<Ruta>> GetHistoryByUserAsync(Guid usuarioId);
    Task<PagedResult<Ruta>> GetByUserPagedAsync(Guid usuarioId, int pageSize, string? continuationToken, CancellationToken cancellationToken = default);
    Task<PagedResult<Ruta>> GetFrequentByUserPagedAsync(Guid usuarioId, int pageSize, string? continuationToken, CancellationToken cancellationToken = default);
    Task<PagedResult<Ruta>> GetHistoryByUserPagedAsync(Guid usuarioId, int pageSize, string? continuationToken, CancellationToken cancellationToken = default);
    Task<Ruta?> GetByIdAsync(Guid id);
    Task<Ruta?> GetByIdAsync(Guid usuarioId, Guid id);
    Task<Ruta?> GetSelectedTodayAsync(Guid usuarioId);
    Task AddAsync(Ruta ruta);
    Task UpdateAsync(Ruta ruta);
    Task DeleteAsync(Ruta ruta);
}
