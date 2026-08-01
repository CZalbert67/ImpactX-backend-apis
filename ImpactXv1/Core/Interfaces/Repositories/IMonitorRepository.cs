using ImpactX.Core.Domain;
using ImpactX.Core.Pagination;
using Monitor = ImpactX.Core.Domain.Monitor;

namespace ImpactX.Core.Interfaces.Repositories;

public interface IMonitorRepository
{
    Task<List<Monitor>> GetByUserAsync(Guid usuarioId);
    Task<PagedResult<Monitor>> GetByUserPagedAsync(Guid usuarioId, int pageSize, string? continuationToken, CancellationToken cancellationToken = default);
    Task<Monitor?> GetByIdAsync(Guid id);
    Task<Monitor?> GetByIdAsync(Guid usuarioId, Guid id);
    Task<List<Monitor>> GetActiveByUserAsync(Guid usuarioId);
    Task<int> CountActiveByUserAsync(Guid usuarioId);
    Task<Monitor?> GetByTokenAsync(string token);
    Task<Monitor?> GetByUsuarioYMonitorAsync(Guid usuarioId, Guid monitorUsuarioId);
    Task<bool> ExistsByUsernameAsync(Guid usuarioId, string username);
    Task AddAsync(Monitor monitor);
    Task UpdateAsync(Monitor monitor);
    Task DeleteAsync(Monitor monitor);
}
