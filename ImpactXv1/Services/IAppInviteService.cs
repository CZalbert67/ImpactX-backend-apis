using ImpactX.Models.DTOs;

namespace ImpactX.Services;

public interface IAppInviteService
{
    Task<List<AppInviteDto>> GetInvitesAsync(Guid usuarioId);
    Task<AppInviteDto> CreateAsync(Guid usuarioId, CreateAppInviteRequest request);
    Task<AppInviteDto> GetByTokenAsync(string token);
    Task<AppInviteDto> AcceptAsync(Guid usuarioId, AcceptAppInviteRequest request);
    Task<AppInviteDto> CancelAsync(Guid usuarioId, Guid inviteId);
    Task DeleteAsync(Guid usuarioId, Guid inviteId);
}
