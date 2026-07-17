using ImpactX.Core.Domain;

namespace ImpactX.Core.Interfaces.Repositories;

public interface IViajeRepository
{
    Task<Viaje?> GetByIdAsync(Guid id);
    Task<Viaje?> GetActiveByUserAsync(Guid usuarioId);
    Task<List<Viaje>> GetByUserAsync(Guid usuarioId);
    Task AddAsync(Viaje viaje);
    Task UpdateAsync(Viaje viaje);
    Task AddTelemetryAsync(ViajeTelemetry telemetry);
    Task<List<ViajeTelemetry>> GetTelemetryByViajeAsync(Guid viajeId);
}
