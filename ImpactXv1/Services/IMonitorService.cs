using ImpactX.Core.Exceptions;
using ImpactX.Models.DTOs;

namespace ImpactX.Services;

public interface IMonitorService
{
    Task<List<MonitorDto>> GetMonitorsAsync(Guid usuarioId);
    Task<InviteMonitorResponse> InviteAsync(Guid usuarioId, InviteMonitorRequest request);
    Task ResendInviteAsync(Guid usuarioId, Guid monitorId);
    Task RestoreMonitorAsync(Guid usuarioId, Guid monitorId);
    Task RevokeMonitorAsync(Guid usuarioId, Guid monitorId);
    Task<InvitationInfoDto> GetInvitationByTokenAsync(string token);
    Task AcceptInvitationAsync(string token, Guid monitorUsuarioId);
    Task RejectInvitationAsync(string token, Guid monitorUsuarioId);
}
