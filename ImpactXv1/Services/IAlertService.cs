using ImpactX.Models.DTOs;

namespace ImpactX.Services;

public interface IAlertService
{
    Task<AlertStatusDto> DetectAsync(Guid usuarioId, DetectAlertRequest request);
    Task<AlertStatusDto> SendSosAsync(Guid usuarioId, SosRequest request);
    Task<ConfirmOkResponse> ConfirmOkAsync(Guid usuarioId, Guid alertaId);
    Task<AlertActionResponse> BypassCriticalAsync(Guid usuarioId, Guid alertaId);
    Task<AlertActionResponse> RetryAsync(Guid usuarioId, Guid alertaId);
    Task<AlertStatusDto> GetStatusAsync(Guid usuarioId, Guid alertaId);
    Task<AlertActionResponse> CloseAsync(Guid usuarioId, Guid alertaId, CloseAlertRequest request);
    Task<List<AlertStatusDto>> SyncOfflineAsync(Guid usuarioId, SyncOfflineRequest request);
}
