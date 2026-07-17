using ImpactX.Core.Domain;

namespace ImpactX.Core.Interfaces.Repositories;

public interface IRutaRepository
{
    Task<List<Ruta>> GetByUserAsync(Guid usuarioId);
    Task<List<Ruta>> GetFrequentByUserAsync(Guid usuarioId);
    Task<List<Ruta>> GetHistoryByUserAsync(Guid usuarioId);
    Task<Ruta?> GetByIdAsync(Guid id);
    Task<Ruta?> GetSelectedTodayAsync(Guid usuarioId);
    Task AddAsync(Ruta ruta);
    Task UpdateAsync(Ruta ruta);
    Task DeleteAsync(Ruta ruta);
}
