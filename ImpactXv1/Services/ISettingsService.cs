using ImpactX.Models.DTOs;

namespace ImpactX.Services;

public interface ISettingsService
{
    Task<SettingsResponseDto> GetSettingsAsync(Guid usuarioId);
    Task<SettingsResponseDto> UpdateSettingsAsync(Guid usuarioId, UpdateSettingsRequest request);
    Task<Setup2FaResponse> Setup2FaAsync(Guid usuarioId);
    Task Enable2FaAsync(Guid usuarioId, Enable2FaRequest request);
    Task Disable2FaAsync(Guid usuarioId, Disable2FaRequest request);
}
