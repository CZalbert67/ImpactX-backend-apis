using ImpactX.Core.Domain;
using ImpactX.Core.Exceptions;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Core.Interfaces.Services;
using ImpactX.Core.Notifications;
using ImpactX.Models.DTOs;
using Microsoft.Extensions.Logging;
using Monitor = ImpactX.Core.Domain.Monitor;

namespace ImpactX.Services;

public class NotificationService : INotificationService
{
    private readonly INotificacionRepository _notificacionRepository;
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IMonitorRepository _monitorRepository;
    private readonly IPushNotificationGateway _pushGateway;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        INotificacionRepository notificacionRepository,
        IUsuarioRepository usuarioRepository,
        IMonitorRepository monitorRepository,
        IPushNotificationGateway pushGateway,
        ILogger<NotificationService> logger)
    {
        _notificacionRepository = notificacionRepository;
        _usuarioRepository = usuarioRepository;
        _monitorRepository = monitorRepository;
        _pushGateway = pushGateway;
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

        var gatewayResult = await _pushGateway.SendAsync(
            usuario.FcmToken,
            titulo,
            mensaje,
            datos);

        if (gatewayResult.Success)
        {
            _logger.LogInformation("Notificación push enviada con éxito a usuario {UsuarioId}. Id: {MessageId}",
                usuarioId, gatewayResult.ExternalMessageId);
        }
        else
        {
            _logger.LogWarning("Notificación push fallida para usuario {UsuarioId}: {Status}",
                usuarioId, gatewayResult.Status);
        }
    }

    public async Task<IReadOnlyList<NotificationDispatchResult>> NotifyAlertMonitorsAsync(
        Alerta alerta,
        CancellationToken cancellationToken = default)
    {
        var results = new List<NotificationDispatchResult>();
        var monitores = await _monitorRepository.GetActiveByUserAsync(alerta.UsuarioId);

        if (monitores.Count == 0)
        {
            _logger.LogInformation("Alerta {AlertaId}: no hay monitores activos para notificar", alerta.Id);
            return results;
        }

        var (title, message, data) = BuildAlertPayload(alerta);

        foreach (var monitor in monitores)
        {
            var result = await DispatchToMonitorAsync(monitor, alerta, title, message, data, cancellationToken);
            results.Add(result);
        }

        return results;
    }

    public async Task<IReadOnlyList<NotificationDispatchResult>> RetryAlertNotificationsAsync(
        Alerta alerta,
        CancellationToken cancellationToken = default)
    {
        var results = new List<NotificationDispatchResult>();
        var monitores = await _monitorRepository.GetActiveByUserAsync(alerta.UsuarioId);

        if (monitores.Count == 0)
        {
            _logger.LogInformation("Alerta {AlertaId}: no hay monitores activos para reintentar", alerta.Id);
            return results;
        }

        var (title, message, data) = BuildAlertPayload(alerta);

        foreach (var monitor in monitores)
        {
            var result = await RetryForMonitorAsync(monitor, alerta, title, message, data, cancellationToken);
            results.Add(result);
        }

        return results;
    }

    private Guid? ResolveMonitorUserId(Monitor monitor)
    {
        if (!string.IsNullOrEmpty(monitor.ProfileId) && Guid.TryParse(monitor.ProfileId, out var uid))
            return uid;
        return null;
    }

    private async Task<NotificationDispatchResult> DispatchToMonitorAsync(
        Monitor monitor,
        Alerta alerta,
        string title,
        string message,
        Dictionary<string, string> data,
        CancellationToken cancellationToken)
    {
        var clave = BuildIdempotencyKey(alerta.Id, monitor.ProfileId);
        var recipientUserId = ResolveMonitorUserId(monitor);

        var existente = await _notificacionRepository.GetByIdempotencyKeyAsync(clave, recipientUserId, cancellationToken);
        if (existente != null)
        {
            if (existente.EstadoEnvio == "Enviado")
            {
                _logger.LogInformation("Alerta {AlertaId} ya notificada al monitor {MonitorId}. Omitiendo.", alerta.Id, monitor.Id);
                return new NotificationDispatchResult(existente.Id, existente.UsuarioId, "DuplicadoOmitido", false);
            }

            return await RetryExistingNotificationAsync(existente, monitor, alerta, title, message, data, cancellationToken);
        }

        var (usuarioVinculado, statusNoVinculado) = await ResolveMonitorUserAsync(monitor);
        if (usuarioVinculado == null)
        {
            _logger.LogInformation("Monitor {MonitorId} sin usuario vinculado. No se persiste historial.", monitor.Id);
            return new NotificationDispatchResult(Guid.Empty, Guid.Empty, statusNoVinculado, false);
        }

        if (!usuarioVinculado.IsActive)
        {
            _logger.LogInformation("Usuario {UsuarioId} (monitor {MonitorId}) está inactivo. No se envía push.", usuarioVinculado.Id, monitor.Id);
            return new NotificationDispatchResult(Guid.Empty, usuarioVinculado.Id, "DestinatarioNoVinculado", false);
        }

        if (string.IsNullOrEmpty(usuarioVinculado.FcmToken))
        {
            _logger.LogInformation("Usuario {UsuarioId} (monitor {MonitorId}) sin FcmToken. SinToken.", usuarioVinculado.Id, monitor.Id);
            var notificacion = await CreateHistoryRecordAsync(monitor, alerta, title, message, clave, "SinToken", usuarioVinculado.Id);
            return new NotificationDispatchResult(notificacion.Id, notificacion.UsuarioId, "SinToken", false);
        }

        var history = await CreateHistoryRecordAsync(monitor, alerta, title, message, clave, "Pendiente", usuarioVinculado.Id);

        var gatewayResult = await _pushGateway.SendAsync(
            usuarioVinculado.FcmToken,
            title,
            message,
            data,
            cancellationToken);

        await UpdateHistoryAfterDispatchAsync(history, gatewayResult);

        return new NotificationDispatchResult(
            history.Id,
            history.UsuarioId,
            gatewayResult.Status,
            gatewayResult.Success);
    }

    private async Task<NotificationDispatchResult> RetryForMonitorAsync(
        Monitor monitor,
        Alerta alerta,
        string title,
        string message,
        Dictionary<string, string> data,
        CancellationToken cancellationToken)
    {
        var clave = BuildIdempotencyKey(alerta.Id, monitor.ProfileId);
        var recipientUserId = ResolveMonitorUserId(monitor);

        var existente = await _notificacionRepository.GetByIdempotencyKeyAsync(clave, recipientUserId, cancellationToken);
        if (existente != null)
        {
            if (existente.EstadoEnvio == "Enviado")
            {
                return new NotificationDispatchResult(existente.Id, existente.UsuarioId, "DuplicadoOmitido", false);
            }

            return await RetryExistingNotificationAsync(existente, monitor, alerta, title, message, data, cancellationToken);
        }

        return await DispatchToMonitorAsync(monitor, alerta, title, message, data, cancellationToken);
    }

    private async Task<NotificationDispatchResult> RetryExistingNotificationAsync(
        Notificacion existente,
        Monitor monitor,
        Alerta alerta,
        string title,
        string message,
        Dictionary<string, string> data,
        CancellationToken cancellationToken)
    {
        var (usuarioVinculado, _) = await ResolveMonitorUserAsync(monitor);
        if (usuarioVinculado == null || !usuarioVinculado.IsActive || string.IsNullOrEmpty(usuarioVinculado.FcmToken))
        {
            existente.Intentos++;
            existente.UltimoIntentoEn = DateTime.UtcNow;
            existente.EstadoEnvio = "Fallido";
            await _notificacionRepository.UpdateAsync(existente);
            return new NotificationDispatchResult(existente.Id, existente.UsuarioId, existente.EstadoEnvio, false);
        }

        existente.Intentos++;
        existente.UltimoIntentoEn = DateTime.UtcNow;

        var gatewayResult = await _pushGateway.SendAsync(
            usuarioVinculado.FcmToken,
            title,
            message,
            data,
            cancellationToken);

        existente.EstadoEnvio = gatewayResult.Status;
        if (gatewayResult.Success)
        {
            existente.EnviadoEn = DateTime.UtcNow;
        }
        await _notificacionRepository.UpdateAsync(existente);

        return new NotificationDispatchResult(
            existente.Id,
            existente.UsuarioId,
            gatewayResult.Status,
            gatewayResult.Success);
    }

    private async Task<(Usuario? usuario, string status)> ResolveMonitorUserAsync(Monitor monitor)
    {
        if (string.IsNullOrEmpty(monitor.ProfileId))
        {
            return (null, "DestinatarioNoVinculado");
        }

        if (!Guid.TryParse(monitor.ProfileId, out var usuarioId))
        {
            return (null, "DestinatarioNoVinculado");
        }

        var usuario = await _usuarioRepository.GetByIdAsync(usuarioId);
        if (usuario == null)
        {
            return (null, "DestinatarioNoVinculado");
        }

        return (usuario, "Vinculado");
    }

    private async Task<Notificacion> CreateHistoryRecordAsync(
        Monitor monitor,
        Alerta alerta,
        string title,
        string message,
        string claveIdempotencia,
        string estadoEnvio,
        Guid? usuarioId = null)
    {
        if (usuarioId == null)
        {
            var (usuario, _) = await ResolveMonitorUserAsync(monitor);
            usuarioId = usuario?.Id ?? throw new InvalidOperationException("No se puede crear historial sin destinatario válido.");
        }

        var notificacion = new Notificacion
        {
            UsuarioId = usuarioId.Value,
            AlertaId = alerta.Id,
            Titulo = title,
            Mensaje = message,
            Tipo = "Alerta",
            ReferenciaId = alerta.Id.ToString(),
            ReferenciaTipo = "Alerta",
            Canal = "Push",
            EstadoEnvio = estadoEnvio,
            Intentos = 1,
            CreadoEn = DateTime.UtcNow,
            UltimoIntentoEn = DateTime.UtcNow,
            ClaveIdempotencia = claveIdempotencia,
        };

        if (estadoEnvio == "Enviado")
        {
            notificacion.EnviadoEn = DateTime.UtcNow;
        }

        await _notificacionRepository.AddAsync(notificacion);
        return notificacion;
    }

    private async Task UpdateHistoryAfterDispatchAsync(Notificacion notificacion, PushGatewayResult gatewayResult)
    {
        notificacion.EstadoEnvio = gatewayResult.Status;
        notificacion.UltimoIntentoEn = DateTime.UtcNow;

        if (gatewayResult.Success)
        {
            notificacion.EnviadoEn = DateTime.UtcNow;
        }

        await _notificacionRepository.UpdateAsync(notificacion);
    }

    private static (string title, string message, Dictionary<string, string> data) BuildAlertPayload(Alerta alerta)
    {
        var title = alerta.Tipo == "SOS"
            ? "Alerta SOS ImpactX"
            : "Alerta de seguridad ImpactX";

        var message = alerta.Tipo == "SOS"
            ? "Una persona que monitoreas activó una alerta SOS."
            : "Se detectó una alerta de seguridad de una persona que monitoreas.";

        var data = new Dictionary<string, string>
        {
            ["alertId"] = alerta.Id.ToString(),
            ["alertType"] = alerta.Tipo,
            ["severity"] = alerta.Severidad,
            ["createdAt"] = alerta.CreadoEn.ToString("O"),
        };

        return (title, message, data);
    }

    private static string BuildIdempotencyKey(Guid alertaId, string? usuarioId)
    {
        return $"alert:{alertaId}:recipient:{usuarioId}:channel:push";
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
