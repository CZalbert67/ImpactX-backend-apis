using ImpactX.Core.Exceptions;
using ImpactX.Models.DTOs;

namespace ImpactX.Services;

public interface IViajeService
{
    Task<ViajeDto> StartAsync(Guid usuarioId, StartTripRequest request);
    Task<TripActionResponse> PauseAsync(Guid usuarioId, Guid viajeId);
    Task<TripActionResponse> ResumeAsync(Guid usuarioId, Guid viajeId);
    Task<ViajeDto> FinishAsync(Guid usuarioId, Guid viajeId);
    Task<List<TelemetryPointDto>> UpdateTelemetryAsync(Guid usuarioId, Guid viajeId, TelemetryUpdateRequest request);
    Task<ViajeDto?> GetActiveAsync(Guid usuarioId);
}
