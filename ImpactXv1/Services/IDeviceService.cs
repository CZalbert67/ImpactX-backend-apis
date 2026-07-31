using ImpactX.Models.DTOs;

namespace ImpactX.Services;

public interface IDeviceService
{
    Task<List<DeviceDto>> GetDevicesAsync(Guid usuarioId);
    Task UpsertFcmTokenAsync(Guid usuarioId, UpsertDeviceRequest request);
    Task DeleteDeviceAsync(Guid usuarioId, Guid deviceId);
    Task DeleteAllDevicesAsync(Guid usuarioId);
}
