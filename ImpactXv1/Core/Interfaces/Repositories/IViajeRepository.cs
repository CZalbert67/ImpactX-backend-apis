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

    /// <summary>
    /// Point-read de un evento de telemetría por su EventId dentro de un
    /// viaje (partición /viajeId). Devuelve null si no existe. Nunca consulta
    /// de forma global ni cruza particiones.
    /// </summary>
    Task<ViajeTelemetry?> GetTelemetryByEventIdAsync(Guid viajeId, Guid eventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Escritura atómica de un lote de eventos nuevos: todos se persisten o
    /// ninguno (TransactionalBatch en Cosmos; una sola transacción en EF).
    /// Los eventos que ya existen se resuelven como duplicado idéntico o
    /// conflicto real (409). Nunca sobrescribe un EventId existente.
    /// </summary>
    Task<TelemetryBatchWriteResult> AddTelemetryBatchAsync(Guid viajeId, IReadOnlyList<ViajeTelemetry> eventos, CancellationToken cancellationToken = default);

    Task<List<ViajeTelemetry>> GetTelemetryByViajeAsync(Guid viajeId);
    Task<PagedResult<ViajeTelemetry>> GetTelemetryByViajePagedAsync(Guid viajeId, int pageSize, string? continuationToken, CancellationToken cancellationToken = default);
}
