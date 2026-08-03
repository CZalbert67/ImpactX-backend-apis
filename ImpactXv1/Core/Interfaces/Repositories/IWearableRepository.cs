using ImpactX.Core.Domain;
using ImpactX.Core.Pagination;

namespace ImpactX.Core.Interfaces.Repositories;

public interface IWearableRepository
{
    Task<Wearable?> GetByIdAsync(Guid id);
    Task<Wearable?> GetByIdAsync(Guid usuarioId, Guid id);
    Task<Wearable?> GetByUsuarioIdAsync(Guid usuarioId);
    Task<List<Wearable>> GetAllByUsuarioIdAsync(Guid usuarioId);
    Task<PagedResult<Wearable>> GetAllByUsuarioIdPagedAsync(Guid usuarioId, int pageSize, string? continuationToken, CancellationToken cancellationToken = default);
    Task<Wearable?> GetByPairingTokenAsync(string token);
    Task<Wearable?> GetByDispositivoIdAsync(string dispositivoId);
    Task AddAsync(Wearable wearable);
    Task UpdateAsync(Wearable wearable);
    Task DeleteAsync(Wearable wearable);
}
