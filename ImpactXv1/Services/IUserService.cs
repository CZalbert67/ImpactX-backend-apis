using Prueba1.Models.DTOs;

namespace Prueba1.Services;

public interface IUserService
{
    Task<UserProfileDto> GetProfileAsync(Guid usuarioId);
    Task<UserProfileDto> UpdateProfileAsync(Guid usuarioId, UpdateUserProfileRequest request);
    Task<UserPreferencesDto> GetPreferencesAsync(Guid usuarioId);
    Task<UserPreferencesDto> UpdatePreferencesAsync(Guid usuarioId, UpdateUserPreferencesRequest request);
    Task<DriverProfileDto> GetDriverProfileAsync(Guid usuarioId);
    Task<DriverProfileDto> UpdateDriverProfileAsync(Guid usuarioId, UpdateDriverProfileRequest request);
    Task<MedicalProfileDto> GetMedicalProfileAsync(Guid usuarioId);
    Task<MedicalProfileDto> UpdateMedicalProfileAsync(Guid usuarioId, UpdateMedicalProfileRequest request);
    Task<List<UserSearchResultDto>> SearchUsersAsync(string query, Guid? excludeUserId = null);
}
