using ImpactX.Core.Domain;
using ImpactX.Core.Exceptions;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Models.DTOs;

namespace ImpactX.Services;

public class NotificationService : INotificationService
{
    private readonly INotificacionRepository _notificacionRepository;
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        INotificacionRepository notificacionRepository,
        IUsuarioRepository usuarioRepository,
        ILogger<NotificationService> logger)
    {
        _notificacionRepository = notificacionRepository;
        _usuarioRepository = usuarioRepository;
        _logger = logger;
    }

    public async Task<List<NotificacionDto>> GetNotificationsAsync(Guid usuarioId)
    {
        var notificaciones = await _notificacionRepository.GetByUserAsync(usuarioId);
        return notificaciones.Select(MapToDto).ToList();
    }

    public async Task<int> GetUnreadCountAsync(Guid usuarioId)
    {
        return await _notificacionRepository.CountUnreadByUserAsync(usuarioId);
    }

    public async Task ToggleReadAsync(Guid usuarioId, Guid notificacionId, ToggleReadRequest request)
    {
        var notificacion = await _notificacionRepository.GetByIdAsync(notificacionId)
            ?? throw new NotFoundException("Notificación no encontrada.");

        if (notificacion.UsuarioId != usuarioId)
            throw new ForbiddenException("No tienes permiso para modificar esta notificación.");

        notificacion.Leida = request.Leida;
        notificacion.LeidaEn = request.Leida ? DateTime.UtcNow : null;

        await _notificacionRepository.UpdateAsync(notificacion);
        _logger.LogInformation("Notificación {NotificacionId} marcada como {Estado} por usuario {UsuarioId}",
            notificacionId, request.Leida ? "leída" : "no leída", usuarioId);
    }

    public async Task MarkAllAsReadAsync(Guid usuarioId)
    {
        var count = await _notificacionRepository.CountUnreadByUserAsync(usuarioId);
        await _notificacionRepository.MarkAllAsReadAsync(usuarioId);
        _logger.LogInformation("{Count} notificaciones marcadas como leídas para usuario {UsuarioId}",
            count, usuarioId);
    }

    public async Task DeleteAsync(Guid usuarioId, Guid notificacionId)
    {
        var notificacion = await _notificacionRepository.GetByIdAsync(notificacionId)
            ?? throw new NotFoundException("Notificación no encontrada.");

        if (notificacion.UsuarioId != usuarioId)
            throw new ForbiddenException("No tienes permiso para eliminar esta notificación.");

        await _notificacionRepository.DeleteAsync(notificacion);
        _logger.LogInformation("Notificación {NotificacionId} eliminada por usuario {UsuarioId}",
            notificacionId, usuarioId);
    }

    public async Task DeleteAllAsync(Guid usuarioId)
    {
        await _notificacionRepository.DeleteAllByUserAsync(usuarioId);
        _logger.LogInformation("Todas las notificaciones eliminadas para usuario {UsuarioId}", usuarioId);
    }

    public async Task SendPushNotificationAsync(
        Guid usuarioId, 
        string titulo, 
        string mensaje, 
        Dictionary<string, string>? datos = null)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(usuarioId);
        if (usuario == null)
        {
            _logger.LogWarning("Intento de enviar notificación push a usuario inexistente: {UsuarioId}", usuarioId);
            return;
        }

        if (string.IsNullOrEmpty(usuario.FcmToken))
        {
            _logger.LogInformation("Usuario {UsuarioId} no tiene token FCM registrado. Saltando push.", usuarioId);
            return;
        }

        try
        {
            if (FirebaseAdmin.FirebaseApp.DefaultInstance == null)
            {
                _logger.LogWarning("FirebaseApp no está inicializado. Saltando envío de push para usuario {UsuarioId}.", usuarioId);
                return;
            }

            var message = new FirebaseAdmin.Messaging.Message()
            {
                Token = usuario.FcmToken,
                Notification = new FirebaseAdmin.Messaging.Notification()
                {
                    Title = titulo,
                    Body = mensaje
                },
                Data = datos
            };

            var response = await FirebaseAdmin.Messaging.FirebaseMessaging.DefaultInstance.SendAsync(message);
            _logger.LogInformation("Notificación push enviada con éxito a usuario {UsuarioId}. Id: {Response}", usuarioId, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al enviar notificación push de Firebase para usuario {UsuarioId}", usuarioId);
        }
    }

    private static NotificacionDto MapToDto(Notificacion n) => new()
    {
        Id = n.Id,
        Titulo = n.Titulo,
        Mensaje = n.Mensaje,
        Tipo = n.Tipo,
        ReferenciaId = n.ReferenciaId,
        ReferenciaTipo = n.ReferenciaTipo,
        Leida = n.Leida,
        LeidaEn = n.LeidaEn,
        CreadoEn = n.CreadoEn,
    };
}
