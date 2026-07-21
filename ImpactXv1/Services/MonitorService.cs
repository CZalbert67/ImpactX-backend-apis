using ImpactX.Core.Exceptions;
using Monitor = ImpactX.Core.Domain.Monitor;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Models.DTOs;

namespace ImpactX.Services;

public class MonitorService : IMonitorService
{
    private readonly IMonitorRepository _monitorRepository;
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IPlanService _planService;
    private readonly ILogger<MonitorService> _logger;

    public MonitorService(
        IMonitorRepository monitorRepository,
        IUsuarioRepository usuarioRepository,
        IPlanService planService,
        ILogger<MonitorService> logger)
    {
        _monitorRepository = monitorRepository;
        _usuarioRepository = usuarioRepository;
        _planService = planService;
        _logger = logger;
    }

    public async Task<List<MonitorDto>> GetMonitorsAsync(Guid usuarioId)
    {
        var monitors = await _monitorRepository.GetByUserAsync(usuarioId);
        return monitors.Select(MapToDto).ToList();
    }

    public async Task<InviteMonitorResponse> InviteAsync(Guid usuarioId, InviteMonitorRequest request)
    {
        var suscripcion = await _planService.GetCurrentSubscriptionAsync(usuarioId);
        var planName = suscripcion?.PlanNombre ?? "Free";

        var maxMonitores = planName switch
        {
            "Premium" => 5,
            "Basic" => 2,
            _ => 0,
        };

        var activeCount = await _monitorRepository.CountActiveByUserAsync(usuarioId);
        if (activeCount >= maxMonitores)
            throw new ConflictException(
                $"Límite de monitores alcanzado ({maxMonitores}). Actualiza tu plan para agregar más.");

        if (string.IsNullOrWhiteSpace(request.Username) && string.IsNullOrWhiteSpace(request.AppUserId))
            throw new BadRequestException("Debes proporcionar un Username o AppUserId del usuario a invitar.");

        Usuario? invitado = null;

        if (!string.IsNullOrWhiteSpace(request.Username))
        {
            invitado = await _usuarioRepository.GetByUsernameAsync(request.Username);
            var exists = await _monitorRepository.ExistsByUsernameAsync(usuarioId, request.Username);
            if (exists)
                throw new ConflictException("Este usuario ya es monitor o tiene una invitación pendiente.");
        }
        else if (!string.IsNullOrWhiteSpace(request.AppUserId))
        {
            var users = await _usuarioRepository.SearchAsync(request.AppUserId);
            invitado = users.FirstOrDefault(u => u.AppId == request.AppUserId);
        }

        if (invitado is null)
            throw new NotFoundException("Usuario no encontrado.");

        if (invitado.Id == usuarioId)
            throw new ConflictException("No puedes invitarte a ti mismo como monitor.");

        var token = Guid.NewGuid().ToString("N")[..12].ToUpper();
        var monitor = new Monitor
        {
            UsuarioId = usuarioId,
            CorreoInvitado = invitado.Correo,
            Username = invitado.Username,
            AppUserId = invitado.AppId,
            ProfileId = invitado.Id.ToString(),
            Estado = "Pendiente",
            TokenInvitacion = token,
            CreadoEn = DateTime.UtcNow,
            Expiracion = DateTime.UtcNow.AddDays(7),
            Permisos = request.Permisos ?? [],
        };

        await _monitorRepository.AddAsync(monitor);

        _logger.LogInformation("Monitor invitado {Username} para usuario {UsuarioId}", invitado.Username, usuarioId);

        return new InviteMonitorResponse
        {
            MonitorId = monitor.Id,
            Token = token,
            Mensaje = $"Invitación enviada a {invitado.Username}.",
        };
    }

    public async Task ResendInviteAsync(Guid usuarioId, Guid monitorId)
    {
        var monitor = await _monitorRepository.GetByIdAsync(monitorId)
            ?? throw new NotFoundException("Invitación no encontrada.");

        if (monitor.UsuarioId != usuarioId)
            throw new ForbiddenException("No tienes permiso para modificar esta invitación.");

        if (monitor.Estado != "Pendiente")
            throw new ConflictException("Solo se pueden reenviar invitaciones pendientes.");

        monitor.TokenInvitacion = Guid.NewGuid().ToString("N")[..12].ToUpper();
        monitor.Expiracion = DateTime.UtcNow.AddDays(7);
        await _monitorRepository.UpdateAsync(monitor);
    }

    public async Task RestoreMonitorAsync(Guid usuarioId, Guid monitorId)
    {
        var monitor = await _monitorRepository.GetByIdAsync(monitorId)
            ?? throw new NotFoundException("Monitor no encontrado.");

        if (monitor.UsuarioId != usuarioId)
            throw new ForbiddenException("No tienes permiso para modificar este monitor.");

        if (monitor.Estado != "Revocado")
            throw new ConflictException("Solo se pueden restaurar monitores revocados.");

        monitor.Estado = "Activo";
        monitor.RevocadoEn = null;
        await _monitorRepository.UpdateAsync(monitor);
    }

    public async Task RevokeMonitorAsync(Guid usuarioId, Guid monitorId)
    {
        var monitor = await _monitorRepository.GetByIdAsync(monitorId)
            ?? throw new NotFoundException("Monitor no encontrado.");

        if (monitor.UsuarioId != usuarioId)
            throw new ForbiddenException("No tienes permiso para eliminar este monitor.");

        if (monitor.Estado != "Activo" && monitor.Estado != "Pendiente")
            throw new ConflictException("Este monitor ya no está activo.");

        monitor.Estado = "Revocado";
        monitor.RevocadoEn = DateTime.UtcNow;
        await _monitorRepository.UpdateAsync(monitor);

        _logger.LogWarning("Monitor {MonitorId} revocado por usuario {UsuarioId}", monitorId, usuarioId);
    }

    public async Task<InvitationInfoDto> GetInvitationByTokenAsync(string token)
    {
        var monitor = await _monitorRepository.GetByTokenAsync(token)
            ?? throw new NotFoundException("Token de invitación inválido o expirado.");

        if (monitor.Estado != "Pendiente")
            throw new ConflictException("Esta invitación ya fue procesada.");

        if (monitor.Expiracion.HasValue && DateTime.UtcNow > monitor.Expiracion.Value)
            throw new ConflictException("El token de invitación ha expirado.");

        return new InvitationInfoDto
        {
            Id = monitor.Id,
            UsuarioId = monitor.UsuarioId,
            CorreoInvitado = monitor.CorreoInvitado,
            Username = monitor.Username,
            AppUserId = monitor.AppUserId,
            Estado = monitor.Estado,
            CreadoEn = monitor.CreadoEn,
            Expiracion = monitor.Expiracion,
        };
    }

    public async Task AcceptInvitationAsync(string token, Guid monitorUsuarioId)
    {
        var monitor = await _monitorRepository.GetByTokenAsync(token)
            ?? throw new NotFoundException("Token de invitación inválido o expirado.");

        if (monitor.ProfileId != monitorUsuarioId.ToString())
            throw new ForbiddenException("Esta invitación no está dirigida a este usuario.");

        if (monitor.Estado != "Pendiente")
            throw new ConflictException("Esta invitación ya fue procesada.");

        if (monitor.Expiracion.HasValue && DateTime.UtcNow > monitor.Expiracion.Value)
            throw new ConflictException("El token de invitación ha expirado.");

        monitor.Estado = "Activo";
        monitor.ConfirmadoEn = DateTime.UtcNow;
        monitor.TokenInvitacion = null;
        await _monitorRepository.UpdateAsync(monitor);

        _logger.LogInformation("Invitación {Token} aceptada por usuario {MonitorUsuarioId}", token, monitorUsuarioId);
    }

    public async Task RejectInvitationAsync(string token, Guid monitorUsuarioId)
    {
        var monitor = await _monitorRepository.GetByTokenAsync(token)
            ?? throw new NotFoundException("Token de invitación inválido o expirado.");

        if (monitor.ProfileId != monitorUsuarioId.ToString())
            throw new ForbiddenException("Esta invitación no está dirigida a este usuario.");

        if (monitor.Estado != "Pendiente")
            throw new ConflictException("Esta invitación ya fue procesada.");

        monitor.Estado = "Rechazado";
        monitor.TokenInvitacion = null;
        await _monitorRepository.UpdateAsync(monitor);
    }

    private static MonitorDto MapToDto(Monitor m) => new()
    {
        Id = m.Id,
        CorreoInvitado = m.CorreoInvitado,
        Username = m.Username,
        AppUserId = m.AppUserId,
        ProfileId = m.ProfileId,
        Estado = m.Estado,
        CreadoEn = m.CreadoEn,
        ConfirmadoEn = m.ConfirmadoEn,
        RevocadoEn = m.RevocadoEn,
        Permisos = m.Permisos,
    };
}
