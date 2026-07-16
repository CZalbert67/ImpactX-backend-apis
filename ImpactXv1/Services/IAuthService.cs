using Prueba1.Models.DTOs;

namespace Prueba1.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> RecoverPasswordAsync(RecoverPasswordRequest request);
    Task<AuthResponse> ResetPasswordAsync(ResetPasswordRequest request);
    Task<AuthResponse> ChangePasswordAsync(Guid usuarioId, ChangePasswordRequest request);
    Task<AuthResponse> LogoutAsync(Guid usuarioId, string refreshToken);
    Task<List<SessionDto>> GetSessionsAsync(Guid usuarioId);
    Task DeleteSessionAsync(Guid usuarioId, Guid sessionId);
    Task DeleteAccountAsync(Guid usuarioId);
    Task<ExportAccountDto> ExportAccountAsync(Guid usuarioId);
}