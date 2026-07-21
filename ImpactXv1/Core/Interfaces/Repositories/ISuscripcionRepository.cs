using ImpactX.Core.Domain;

namespace ImpactX.Core.Interfaces.Repositories;

public interface ISuscripcionRepository
{
    Task<Suscripcion?> GetActiveByUserAsync(Guid usuarioId);
    Task<List<Suscripcion>> GetHistoryByUserAsync(Guid usuarioId);
    Task<Suscripcion?> GetByIdAsync(Guid id);
    Task AddAsync(Suscripcion suscripcion);
    Task UpdateAsync(Suscripcion suscripcion);
    Task<List<Suscripcion>> GetExpiredAsync();
    Task<List<Suscripcion>> GetTrialsEndingAsync(int daysRemaining);
}
