using ImpactX.Core.Exceptions;
using ImpactX.Core.Pagination;
using ImpactX.Core.Security;
using ImpactX.Models.DTOs;

namespace ImpactX.Services;

public interface IViajeService
{
    Task<ViajeDto> StartAsync(Guid usuarioId, StartTripRequest request);
    Task<TripActionResponse> PauseAsync(Guid usuarioId, Guid viajeId, string client = ClientTypePolicy.Wearable);
    Task<TripActionResponse> ResumeAsync(Guid usuarioId, Guid viajeId, string client = ClientTypePolicy.Wearable);
    Task<ViajeDto> FinishAsync(Guid usuarioId, Guid viajeId, string client = ClientTypePolicy.Wearable);
    Task<List<TelemetryPointDto>> UpdateTelemetryAsync(Guid usuarioId, Guid viajeId, TelemetryUpdateRequest request);
    Task<TelemetryIngestionResultDto> IngestTelemetryAsync(
        Guid usuarioId,
        Guid viajeId,
        TelemetryBatchRequest request,
        CancellationToken cancellationToken = default,
        string client = ClientTypePolicy.Wearable);
    Task<ViajeDto?> GetActiveAsync(Guid usuarioId);
    Task<PagedResult<ViajeDto>> GetTripsPagedAsync(Guid usuarioId, int? pageSize, string? continuationToken);
    Task<PagedResult<TelemetryPointDto>> GetTelemetryPagedAsync(Guid usuarioId, Guid viajeId, int? pageSize, string? continuationToken);
}
