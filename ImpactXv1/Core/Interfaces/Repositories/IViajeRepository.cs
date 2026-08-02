using ImpactX.Core.Domain;
using ImpactX.Core.Pagination;

namespace ImpactX.Core.Interfaces.Repositories;

public interface IViajeRepository
{
    Task<Viaje?> GetByIdAsync(Guid id);
    Task<Viaje?> GetByIdAsync(Guid usuarioId, Guid id);
    Task<Viaje?> GetActiveByUserAsync(Guid usuarioId);
    Task<List<Viaje>> GetByUserAsync(Guid usuarioId);
    Task<PagedResult<Viaje>> GetByUserPagedAsync(Guid usuarioId, int pageSize, string? continuationToken, CancellationToken cancellationToken = default);
    Task AddAsync(Viaje viaje);
    Task UpdateAsync(Viaje viaje);
    Task AddTelemetryAsync(ViajeTelemetry telemetry);
    Task<List<ViajeTelemetry>> GetTelemetryByViajeAsync(Guid viajeId);
    Task<PagedResult<ViajeTelemetry>> GetTelemetryByViajePagedAsync(Guid viajeId, int pageSize, string? continuationToken, CancellationToken cancellationToken = default);
}
