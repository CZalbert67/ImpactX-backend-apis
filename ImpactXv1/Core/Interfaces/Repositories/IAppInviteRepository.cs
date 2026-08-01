using ImpactX.Core.Domain;

namespace ImpactX.Core.Interfaces.Repositories;

public interface IAppInviteRepository
{
    Task<List<AppInvite>> GetByUserAsync(Guid usuarioId);
    Task<AppInvite?> GetByIdAsync(Guid id);
    Task<AppInvite?> GetByTokenAsync(string token);
    Task<int> CountPendingByUserAsync(Guid usuarioId);
    Task AddAsync(AppInvite invite);
    Task UpdateAsync(AppInvite invite);
    Task DeleteAsync(AppInvite invite);
}
