using ImpactX.Core.Pagination;
using ImpactX.Models.DTOs;

namespace ImpactX.Services;

public interface IWearableService
{
    Task<WearableDto?> GetWearableAsync(Guid usuarioId);
    Task<PagedResult<WearableDto>> GetWearablesPagedAsync(Guid usuarioId, int? pageSize, string? continuationToken);
    Task<PairResponse> PairAsync(Guid usuarioId, PairWearableRequest request);
    Task<WearableDto> PairConfirmAsync(Guid usuarioId, PairConfirmRequest request);
    Task<List<TelemetryPointDto>> SyncAsync(Guid usuarioId, SyncTelemetryRequest request);
    Task<WearableDto> CalibrateAsync(Guid usuarioId, CalibrationRequest request);
    Task UnlinkAsync(Guid usuarioId);
    Task<WearableDto> UpdatePermissionsAsync(Guid usuarioId, UpdateWearablePermissionsRequest request);
    Task<SensorDiagnosticsDto> GetSensorDiagnosticsAsync(Guid usuarioId);
    Task<WearableDto> UpdateBatteryAsync(Guid usuarioId, BatteryUpdateRequest request);
    Task<WearableDto> RegisterHeartbeatAsync(Guid usuarioId, WearableHeartbeatRequest request);
    Task<WearableDto> ReportDiagnosticsAsync(Guid usuarioId, WearableDiagnosticsReportRequest request);
}
