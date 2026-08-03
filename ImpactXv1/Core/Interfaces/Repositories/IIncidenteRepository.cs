using ImpactX.Core.Domain;

namespace ImpactX.Core.Interfaces.Repositories;

public interface IIncidenteRepository
{
    Task<Incidente?> GetByIdAsync(Guid id);
    Task<Incidente?> GetByIdAsync(Guid usuarioId, Guid id);
    Task<Incidente?> GetByAlertIdAsync(Guid usuarioId, Guid alertaId);
    Task<List<Incidente>> GetByUserAsync(Guid usuarioId);
    Task<List<Incidente>> GetFilteredAsync(Guid usuarioId, string? severidad, DateTime? desde, DateTime? hasta, int pagina, int tamano);
    Task<int> CountFilteredAsync(Guid usuarioId, string? severidad, DateTime? desde, DateTime? hasta);
    Task AddAsync(Incidente incidente);
    Task UpdateAsync(Incidente incidente);
    Task<int> CountByUserAsync(Guid usuarioId);
}
