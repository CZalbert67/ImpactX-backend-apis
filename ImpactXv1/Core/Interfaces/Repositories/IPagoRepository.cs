using ImpactX.Core.Domain;
using ImpactX.Core.Pagination;

namespace ImpactX.Core.Interfaces.Repositories;

public interface IPagoRepository
{
    Task<List<Pago>> GetByUserAsync(Guid usuarioId);
    Task<PagedResult<Pago>> GetByUserPagedAsync(Guid usuarioId, int pageSize, string? continuationToken, CancellationToken cancellationToken = default);
    Task<Pago?> GetByIdAsync(Guid id);
    Task<Pago?> GetByIdAsync(Guid usuarioId, Guid id);
    Task AddAsync(Pago pago);
    Task UpdateAsync(Pago pago);
}
