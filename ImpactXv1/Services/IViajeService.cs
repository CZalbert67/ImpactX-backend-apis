using ImpactX.Core.Exceptions;
using ImpactX.Core.Pagination;
using ImpactX.Models.DTOs;

namespace ImpactX.Services;

public interface IViajeService
{
    Task<ViajeDto> StartAsync(Guid usuarioId, StartTripRequest request);
    Task<TripActionResponse> PauseAsync(Guid usuarioId, Guid viajeId);
    Task<TripActionResponse> ResumeAsync(Guid usuarioId, Guid viajeId);
    Task<ViajeDto> FinishAsync(Guid usuarioId, Guid viajeId);
    Task<List<TelemetryPointDto>> UpdateTelemetryAsync(Guid usuarioId, Guid viajeId, TelemetryUpdateRequest request);
    Task<TelemetryIngestionResultDto> IngestTelemetryAsync(Guid usuarioId, Guid viajeId, TelemetryBatchRequest request, CancellationToken cancellationToken = default);
    Task<ViajeDto?> GetActiveAsync(Guid usuarioId);
    Task<PagedResult<ViajeDto>> GetTripsPagedAsync(Guid usuarioId, int? pageSize, string? continuationToken);
    Task<PagedResult<TelemetryPointDto>> GetTelemetryPagedAsync(Guid usuarioId, Guid viajeId, int? pageSize, string? continuationToken);
}
