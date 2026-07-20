using ImpactX.Core.Domain;

namespace ImpactX.Core.Interfaces.Repositories;

public interface IAlertaRepository
{
    Task<Alerta?> GetByIdAsync(Guid id);
    Task<List<Alerta>> GetByUserAsync(Guid usuarioId);
    Task<Alerta?> GetActiveByUserAsync(Guid usuarioId);
    Task<List<Alerta>> GetPendingByUserAsync(Guid usuarioId);
    Task<List<Alerta>> GetActiveAlertsAsync(Guid usuarioId);
    Task AddAsync(Alerta alerta);
    Task UpdateAsync(Alerta alerta);
    Task<int> CountByUserAsync(Guid usuarioId);
}
