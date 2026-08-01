using System.Security.Cryptography;
using ImpactX.Core.Domain;
using ImpactX.Core.Exceptions;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Models.DTOs;
using Microsoft.Extensions.Logging;

namespace ImpactX.Services;

public class AppInviteService : IAppInviteService
{
    private const string PendingStatus = "Pendiente de registro";

    private readonly IAppInviteRepository _appInviteRepository;
    private readonly ILogger<AppInviteService> _logger;

    public AppInviteService(
        IAppInviteRepository appInviteRepository,
        ILogger<AppInviteService> logger)
    {
        _appInviteRepository = appInviteRepository;
        _logger = logger;
    }

    public async Task<List<AppInviteDto>> GetInvitesAsync(Guid usuarioId)
    {
        var invites = await _appInviteRepository.GetByUserAsync(usuarioId);
        var now = DateTime.UtcNow;

        foreach (var invite in invites)
        {
            if (invite.Status == PendingStatus && invite.ExpiresAt.HasValue && now > invite.ExpiresAt.Value)
            {
                invite.Status = "Expirada";
                await _appInviteRepository.UpdateAsync(invite);
            }
        }

        return invites.Select(MapToDto).ToList();
    }

    public async Task<AppInviteDto> CreateAsync(Guid usuarioId, CreateAppInviteRequest request)
    {
        var token = BuildToken();
        var invite = new AppInvite
        {
            UsuarioId = usuarioId,
            Token = token,
            SuggestedUsername = request.SuggestedUsername,
            Relation = request.Relation,
            Priority = request.Priority,
            Status = PendingStatus,
            PersonalMessage = request.PersonalMessage,
            AutoAddToNetwork = request.AutoAddToNetwork,
            InviteUrl = $"https://impactx.app/invite/{token}",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
        };

        await _appInviteRepository.AddAsync(invite);

        _logger.LogInformation("Invitación de app {InviteId} creada para usuario {UsuarioId}", invite.Id, usuarioId);

        return MapToDto(invite);
    }

    public async Task<AppInviteDto> GetByTokenAsync(string token)
    {
        var invite = await _appInviteRepository.GetByTokenAsync(token)
            ?? throw new NotFoundException("Invitación inválida o expirada.");

        ValidateActiveAsync(invite);

        return MapToDto(invite);
    }

    public async Task<AppInviteDto> AcceptAsync(Guid usuarioId, AcceptAppInviteRequest request)
    {
        var invite = await _appInviteRepository.GetByTokenAsync(request.Token)
            ?? throw new NotFoundException("Invitación inválida o expirada.");

        ValidateActiveAsync(invite);

        invite.Status = "Aceptado";
        await _appInviteRepository.UpdateAsync(invite);

        _logger.LogInformation("Invitación {InviteId} aceptada por usuario {UsuarioId}", invite.Id, usuarioId);

        return MapToDto(invite);
    }

    public async Task<AppInviteDto> CancelAsync(Guid usuarioId, Guid inviteId)
    {
        var invite = await GetOwnedInviteAsync(usuarioId, inviteId);

        if (invite.Status != PendingStatus)
            throw new ConflictException("Solo se pueden cancelar invitaciones pendientes.");

        invite.Status = "Cancelado";
        await _appInviteRepository.UpdateAsync(invite);

        _logger.LogInformation("Invitación {InviteId} cancelada por usuario {UsuarioId}", inviteId, usuarioId);

        return MapToDto(invite);
    }

    public async Task DeleteAsync(Guid usuarioId, Guid inviteId)
    {
        var invite = await GetOwnedInviteAsync(usuarioId, inviteId);

        await _appInviteRepository.DeleteAsync(invite);

        _logger.LogWarning("Invitación {InviteId} eliminada por usuario {UsuarioId}", inviteId, usuarioId);
    }

    private async Task<AppInvite> GetOwnedInviteAsync(Guid usuarioId, Guid inviteId)
    {
        var invite = await _appInviteRepository.GetByIdAsync(inviteId)
            ?? throw new NotFoundException("Invitación no encontrada.");

        if (invite.UsuarioId != usuarioId)
            throw new ForbiddenException("No tienes permiso para gestionar esta invitación.");

        return invite;
    }

    private static void ValidateActiveAsync(AppInvite invite)
    {
        if (invite.Status != PendingStatus)
            throw new ConflictException("Esta invitación ya fue procesada.");

        if (invite.ExpiresAt.HasValue && DateTime.UtcNow > invite.ExpiresAt.Value)
            throw new ConflictException("El token de invitación ha expirado.");
    }

    private static string BuildToken()
    {
        var suffix = Convert.ToBase64String(RandomNumberGenerator.GetBytes(6))
            .TrimEnd('=')
            .Replace('+', 'A')
            .Replace('/', 'B')
            .ToUpperInvariant();

        var prefix = new string(Enumerable.Range(0, 3)
            .Select(_ => (char)RandomNumberGenerator.GetInt32(65, 91))
            .ToArray());

        return $"INV-{prefix}-{suffix[..4]}";
    }

    private static AppInviteDto MapToDto(AppInvite i) => new()
    {
        Id = i.Id,
        UsuarioId = i.UsuarioId,
        Token = i.Token,
        SuggestedUsername = i.SuggestedUsername,
        Relation = i.Relation,
        Priority = i.Priority,
        Status = i.Status,
        PersonalMessage = i.PersonalMessage,
        AutoAddToNetwork = i.AutoAddToNetwork,
        InviteUrl = i.InviteUrl,
        CreatedAt = i.CreatedAt,
        ExpiresAt = i.ExpiresAt,
    };
}
