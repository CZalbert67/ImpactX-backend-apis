using ImpactX.Core.Notifications;
using ImpactX.Core.Domain;
using ImpactX.Core.Domain.Enums;
using ImpactX.Core.Exceptions;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Core.Interfaces.Services;
using ImpactX.Core.Pagination;
using ImpactX.Models.DTOs;
using Microsoft.Extensions.Logging;

namespace ImpactX.Services;

public class NotificationService : INotificationService
{
    private readonly INotificacionRepository _notificacionRepository;
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IDispositivoRepository _dispositivoRepository;
    private readonly IMonitoringRelationshipRepository _monitoringRelationshipRepository;
    private readonly IPushNotificationGateway _pushGateway;
    private readonly IFamilySubscriptionRepository? _familySubscriptionRepository;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        INotificacionRepository notificacionRepository,
        IUsuarioRepository usuarioRepository,
        IDispositivoRepository dispositivoRepository,
        IMonitoringRelationshipRepository monitoringRelationshipRepository,
        IPushNotificationGateway pushGateway,
        ILogger<NotificationService> logger,
        IFamilySubscriptionRepository? familySubscriptionRepository = null)
    {
        _notificacionRepository = notificacionRepository;
        _usuarioRepository = usuarioRepository;
        _dispositivoRepository = dispositivoRepository;
        _monitoringRelationshipRepository = monitoringRelationshipRepository;
        _pushGateway = pushGateway;
        _logger = logger;
        _familySubscriptionRepository = familySubscriptionRepository;
    }

    public async Task<List<NotificacionDto>> GetNotificationsAsync(Guid usuarioId)
    {
        var notificaciones = await _notificacionRepository.GetByUserAsync(usuarioId);
        return notificaciones.Select(MapToDto).ToList();
    }

    public async Task<PagedResult<NotificacionDto>> GetNotificationsPagedAsync(Guid usuarioId, int? pageSize, string? continuationToken)
    {
        var size = PaginationValidator.Resolve(pageSize, continuationToken);
        var page = await _notificacionRepository.GetByUserPagedAsync(usuarioId, size, continuationToken);
        return new PagedResult<NotificacionDto>
        {
            Items = page.Items.Select(MapToDto).ToList(),
            ContinuationToken = page.ContinuationToken,
            HasMoreResults = page.HasMoreResults,
            PageSize = page.PageSize,
        };
    }

    public async Task<int> GetUnreadCountAsync(Guid usuarioId)
    {
        return await _notificacionRepository.CountUnreadByUserAsync(usuarioId);
    }

    public async Task ToggleReadAsync(Guid usuarioId, Guid notificacionId, ToggleReadRequest request)
    {
        var notificacion = await _notificacionRepository.GetByIdAsync(usuarioId, notificacionId)
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
        var notificacion = await _notificacionRepository.GetByIdAsync(usuarioId, notificacionId)
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

        var tokens = await ResolvePushTokensAsync(usuario);
        if (tokens.Count == 0)
        {
            _logger.LogInformation("Usuario {UsuarioId} no tiene dispositivos activos con token FCM. Saltando push.", usuarioId);
            return;
        }

        await SendToTokensAsync(tokens, titulo, mensaje, datos);

        _logger.LogInformation("Notificación push despachada a {Cantidad} token(s) para usuario {UsuarioId}",
            tokens.Count, usuarioId);
    }

    public async Task<NotificacionDto> CreateAndDispatchAsync(
        AppNotificationCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.RecipientUserId == Guid.Empty)
            throw new ArgumentException("El destinatario de la notificación es obligatorio.", nameof(command));

        if (!string.IsNullOrWhiteSpace(command.IdempotencyKey))
        {
            var existing = await _notificacionRepository.GetByIdempotencyKeyAsync(
                command.IdempotencyKey,
                command.RecipientUserId,
                cancellationToken);
            if (existing is not null)
                return MapToDto(existing);
        }

        var notification = new Notificacion
        {
            Id = Guid.NewGuid(),
            UsuarioId = command.RecipientUserId,
            Titulo = command.Title,
            Mensaje = command.Message,
            Tipo = command.Type,
            Evento = command.Event,
            ReferenciaId = command.EntityId,
            ReferenciaTipo = command.ReferenceType,
            EntityId = command.EntityId,
            DeepLink = command.DeepLink,
            PublicRelationshipId = command.PublicRelationshipId,
            Canal = "Internal+Push",
            EstadoEnvio = "Pendiente",
            Intentos = 0,
            CreadoEn = DateTime.UtcNow,
            ClaveIdempotencia = command.IdempotencyKey
        };

        await _notificacionRepository.AddAsync(notification);
        var unreadCount = await _notificacionRepository.CountUnreadByUserAsync(command.RecipientUserId);
        var payload = new Dictionary<string, string>(command.Data ?? new Dictionary<string, string>())
        {
            ["notificationId"] = notification.Id.ToString(),
            ["type"] = command.Type,
            ["event"] = command.Event,
            ["unreadCount"] = unreadCount.ToString(),
            ["contractVersion"] = ImpactX.Core.ApiContract.ApiContractDefinition.ContractVersion
        };
        AddIfPresent(payload, "publicRelationshipId", command.PublicRelationshipId);
        AddIfPresent(payload, "entityId", command.EntityId);
        AddIfPresent(payload, "deepLink", command.DeepLink);

        try
        {
            var recipient = await _usuarioRepository.GetByIdAsync(command.RecipientUserId);
            var tokens = recipient is null ? new List<string>() : await ResolvePushTokensAsync(recipient);
            notification.Intentos = 1;
            notification.UltimoIntentoEn = DateTime.UtcNow;
            if (tokens.Count == 0)
            {
                notification.EstadoEnvio = "SinToken";
            }
            else
            {
                var result = await SendToTokensAsync(tokens, command.Title, command.Message, payload, cancellationToken);
                notification.EstadoEnvio = result.Status;
                if (result.Success)
                    notification.EnviadoEn = DateTime.UtcNow;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            notification.Intentos++;
            notification.UltimoIntentoEn = DateTime.UtcNow;
            notification.EstadoEnvio = "Fallido";
            _logger.LogWarning(ex, "No se pudo enviar push para la notificación {NotificationId}", notification.Id);
        }

        await _notificacionRepository.UpdateAsync(notification);
        return MapToDto(notification);
    }

    public async Task<IReadOnlyList<NotificationDispatchResult>> NotifyAlertMonitorsAsync(
        Alerta alerta,
        CancellationToken cancellationToken = default)
    {
        var relationships = await GetAlertRecipientsAsync(alerta.UsuarioId, cancellationToken);
        if (relationships.Count == 0)
        {
            _logger.LogInformation(
                "Alerta {AlertaId}: no hay relaciones de monitoreo autorizadas para notificar",
                alerta.Id);
            return [];
        }

        var results = new List<NotificationDispatchResult>(relationships.Count);
        var (title, message, data) = BuildAlertPayload(alerta);
        foreach (var relationship in relationships)
        {
            var result = await DispatchToMonitorAsync(
                relationship,
                alerta,
                title,
                message,
                data,
                cancellationToken);
            results.Add(result);
        }

        return results;
    }

    public async Task<IReadOnlyList<NotificationDispatchResult>> RetryAlertNotificationsAsync(
        Alerta alerta,
        CancellationToken cancellationToken = default)
    {
        var relationships = await GetAlertRecipientsAsync(alerta.UsuarioId, cancellationToken);
        if (relationships.Count == 0)
        {
            _logger.LogInformation(
                "Alerta {AlertaId}: no hay relaciones de monitoreo autorizadas para reintentar",
                alerta.Id);
            return [];
        }

        var results = new List<NotificationDispatchResult>(relationships.Count);
        var (title, message, data) = BuildAlertPayload(alerta);
        foreach (var relationship in relationships)
        {
            var result = await RetryForMonitorAsync(
                relationship,
                alerta,
                title,
                message,
                data,
                cancellationToken);
            results.Add(result);
        }

        return results;
    }

    private async Task<IReadOnlyList<MonitoringRelationship>> GetAlertRecipientsAsync(
        Guid monitoredUserId,
        CancellationToken cancellationToken)
    {
        var groupRecipients = new List<(MonitoringRelationship Relationship, int Priority)>();
        if (_familySubscriptionRepository is not null)
        {
            var group = await _familySubscriptionRepository.GetActiveByUserAsync(
                monitoredUserId,
                cancellationToken);
            if (group is not null)
            {
                var activeIds = group.Memberships
                    .Where(value => value.Status == FamilyMembershipStatus.Active)
                    .Select(value => value.UserId)
                    .Append(group.OwnerUserId)
                    .Distinct()
                    .ToHashSet();
                groupRecipients.AddRange(group.AccessPolicies
                    .Where(value => value.SubjectUserId == monitoredUserId
                        && activeIds.Contains(value.ViewerUserId)
                        && value.Permissions.ReceiveCriticalAlerts
                        && value.Permissions.ReceiveNotifications)
                    .Select(value => (new MonitoringRelationship
                    {
                        Id = value.Id,
                        PublicRelationshipId = value.PublicRelationshipId,
                        MonitorUserId = value.ViewerUserId,
                        MonitoredUserId = value.SubjectUserId,
                        InitiatedByUserId = group.OwnerUserId,
                        Direction = MonitoringRequestDirection.MonitoredRequestsMonitor,
                        Status = MonitoringRelationshipStatus.Accepted,
                        Permissions = value.Permissions,
                        MedicalConsentGrantedAtUtc = value.MedicalConsentGrantedAtUtc,
                        RequestedAtUtc = value.CreatedAtUtc,
                        AcceptedAtUtc = value.CreatedAtUtc,
                        ExpiresAtUtc = DateTime.MaxValue,
                        UpdatedAtUtc = value.UpdatedAtUtc
                    }, value.SosPriority ?? int.MaxValue)));

                // Compatibilidad de migración: los grupos creados antes del contrato 2026.08.05
                // pueden no tener todavía políticas materializadas. Solo se generan destinatarios
                // predeterminados para pares inexistentes; una política explícita que desactive
                // alertas siempre se respeta.
                var configuredViewers = group.AccessPolicies
                    .Where(value => value.SubjectUserId == monitoredUserId)
                    .Select(value => value.ViewerUserId)
                    .ToHashSet();
                foreach (var viewerUserId in activeIds.Where(value =>
                             value != monitoredUserId && !configuredViewers.Contains(value)))
                {
                    groupRecipients.Add((new MonitoringRelationship
                    {
                        Id = Guid.NewGuid(),
                        PublicRelationshipId = $"GRP-{group.PublicSubscriptionId}-{viewerUserId:N}",
                        MonitorUserId = viewerUserId,
                        MonitoredUserId = monitoredUserId,
                        InitiatedByUserId = group.OwnerUserId,
                        Direction = MonitoringRequestDirection.MonitoredRequestsMonitor,
                        Status = MonitoringRelationshipStatus.Accepted,
                        Permissions = new MonitoringPermissions
                        {
                            ViewRoutes = false,
                            ViewLocation = false,
                            ViewEmergencyLocation = true,
                            ViewIncidents = true,
                            ReceiveCriticalAlerts = true,
                            ViewMedicalProfile = false,
                            SendMessages = true,
                            ViewTelemetry = false,
                            ReceiveNotifications = true
                        },
                        RequestedAtUtc = group.CreatedAtUtc,
                        AcceptedAtUtc = group.CreatedAtUtc,
                        ExpiresAtUtc = DateTime.MaxValue,
                        UpdatedAtUtc = group.UpdatedAtUtc
                    }, int.MaxValue));
                }
            }
        }

        var groupUserIds = groupRecipients
            .Select(value => value.Relationship.MonitorUserId)
            .ToHashSet();
        var legacy = await _monitoringRelationshipRepository
            .GetAcceptedForMonitoredUserAsync(monitoredUserId, cancellationToken);

        return groupRecipients
            .OrderBy(value => value.Priority)
            .ThenBy(value => value.Relationship.AcceptedAtUtc)
            .Select(value => value.Relationship)
            .Concat(legacy
                .Where(value => !groupUserIds.Contains(value.MonitorUserId))
                .GroupBy(value => value.MonitorUserId)
                .Select(group => group.OrderByDescending(value => value.UpdatedAtUtc).First())
                .Where(value => value.Permissions.ReceiveCriticalAlerts
                    && value.Permissions.ReceiveNotifications))
            .ToList();
    }

    private async Task<NotificationDispatchResult> DispatchToMonitorAsync(
        MonitoringRelationship relationship,
        Alerta alerta,
        string title,
        string message,
        Dictionary<string, string> data,
        CancellationToken cancellationToken)
    {
        var recipientUserId = relationship.MonitorUserId;
        var idempotencyKey = BuildIdempotencyKey(alerta.Id, recipientUserId);
        var existing = await _notificacionRepository.GetByIdempotencyKeyAsync(
            idempotencyKey,
            recipientUserId,
            cancellationToken);
        if (existing is not null)
        {
            if (existing.EstadoEnvio == "Enviado")
            {
                _logger.LogInformation(
                    "Alerta {AlertaId} ya notificada mediante relación {PublicRelationshipId}. Omitiendo.",
                    alerta.Id,
                    relationship.PublicRelationshipId);
                return new NotificationDispatchResult(
                    existing.Id,
                    existing.UsuarioId,
                    "DuplicadoOmitido",
                    false);
            }

            return await RetryExistingNotificationAsync(
                existing,
                relationship,
                title,
                message,
                data,
                cancellationToken);
        }

        var recipient = await ResolveMonitorUserAsync(relationship);
        if (recipient is null || !recipient.IsActive)
        {
            _logger.LogInformation(
                "Relación {PublicRelationshipId} sin monitor activo vinculado. No se envía push.",
                relationship.PublicRelationshipId);
            return new NotificationDispatchResult(
                Guid.Empty,
                recipientUserId,
                "DestinatarioNoVinculado",
                false);
        }

        var tokens = await ResolvePushTokensAsync(recipient);
        if (tokens.Count == 0)
        {
            var notification = await CreateHistoryRecordAsync(
                relationship,
                alerta,
                title,
                message,
                idempotencyKey,
                "SinToken",
                recipientUserId);
            return new NotificationDispatchResult(
                notification.Id,
                notification.UsuarioId,
                "SinToken",
                false);
        }

        var history = await CreateHistoryRecordAsync(
            relationship,
            alerta,
            title,
            message,
            idempotencyKey,
            "Pendiente",
            recipientUserId);
        var pushData = await EnrichAlertDataAsync(data, history, relationship);
        var gatewayResult = await SendToTokensAsync(
            tokens,
            title,
            message,
            pushData,
            cancellationToken);
        await UpdateHistoryAfterDispatchAsync(history, gatewayResult);

        return new NotificationDispatchResult(
            history.Id,
            history.UsuarioId,
            gatewayResult.Status,
            gatewayResult.Success);
    }

    private async Task<NotificationDispatchResult> RetryForMonitorAsync(
        MonitoringRelationship relationship,
        Alerta alerta,
        string title,
        string message,
        Dictionary<string, string> data,
        CancellationToken cancellationToken)
    {
        var recipientUserId = relationship.MonitorUserId;
        var idempotencyKey = BuildIdempotencyKey(alerta.Id, recipientUserId);
        var existing = await _notificacionRepository.GetByIdempotencyKeyAsync(
            idempotencyKey,
            recipientUserId,
            cancellationToken);
        if (existing is not null)
        {
            if (existing.EstadoEnvio == "Enviado")
            {
                return new NotificationDispatchResult(
                    existing.Id,
                    existing.UsuarioId,
                    "DuplicadoOmitido",
                    false);
            }

            return await RetryExistingNotificationAsync(
                existing,
                relationship,
                title,
                message,
                data,
                cancellationToken);
        }

        return await DispatchToMonitorAsync(
            relationship,
            alerta,
            title,
            message,
            data,
            cancellationToken);
    }

    private async Task<NotificationDispatchResult> RetryExistingNotificationAsync(
        Notificacion existing,
        MonitoringRelationship relationship,
        string title,
        string message,
        Dictionary<string, string> data,
        CancellationToken cancellationToken)
    {
        var recipient = await ResolveMonitorUserAsync(relationship);
        if (recipient is null || !recipient.IsActive)
        {
            existing.Intentos++;
            existing.UltimoIntentoEn = DateTime.UtcNow;
            existing.EstadoEnvio = "Fallido";
            await _notificacionRepository.UpdateAsync(existing);
            return new NotificationDispatchResult(
                existing.Id,
                existing.UsuarioId,
                existing.EstadoEnvio,
                false);
        }

        var tokens = await ResolvePushTokensAsync(recipient);
        if (tokens.Count == 0)
        {
            existing.Intentos++;
            existing.UltimoIntentoEn = DateTime.UtcNow;
            existing.EstadoEnvio = "Fallido";
            await _notificacionRepository.UpdateAsync(existing);
            return new NotificationDispatchResult(
                existing.Id,
                existing.UsuarioId,
                existing.EstadoEnvio,
                false);
        }

        existing.Intentos++;
        existing.UltimoIntentoEn = DateTime.UtcNow;
        var pushData = await EnrichAlertDataAsync(data, existing, relationship);
        var gatewayResult = await SendToTokensAsync(
            tokens,
            title,
            message,
            pushData,
            cancellationToken);
        existing.EstadoEnvio = gatewayResult.Status;
        if (gatewayResult.Success)
        {
            existing.EnviadoEn = DateTime.UtcNow;
        }

        await _notificacionRepository.UpdateAsync(existing);
        return new NotificationDispatchResult(
            existing.Id,
            existing.UsuarioId,
            gatewayResult.Status,
            gatewayResult.Success);
    }

    private Task<Usuario?> ResolveMonitorUserAsync(MonitoringRelationship relationship)
    {
        return _usuarioRepository.GetByIdAsync(relationship.MonitorUserId);
    }

    private async Task<Notificacion> CreateHistoryRecordAsync(
        MonitoringRelationship relationship,
        Alerta alerta,
        string title,
        string message,
        string idempotencyKey,
        string dispatchStatus,
        Guid recipientUserId)
    {
        var notification = new Notificacion
        {
            UsuarioId = recipientUserId,
            AlertaId = alerta.Id,
            Titulo = title,
            Mensaje = message,
            Tipo = "Alerta",
            ReferenciaId = alerta.Id.ToString(),
            ReferenciaTipo = "Alerta",
            Canal = "Push",
            EstadoEnvio = dispatchStatus,
            Intentos = 1,
            CreadoEn = DateTime.UtcNow,
            UltimoIntentoEn = DateTime.UtcNow,
            ClaveIdempotencia = idempotencyKey,
            PublicRelationshipId = relationship.PublicRelationshipId,
            Evento = alerta.Tipo == "SOS" ? "WearableSosTriggered" : "CriticalAlertCreated",
            EntityId = alerta.Id.ToString(),
            DeepLink = $"/app/alerts?alertId={alerta.Id}"
        };

        if (dispatchStatus == "Enviado")
        {
            notification.EnviadoEn = DateTime.UtcNow;
        }

        await _notificacionRepository.AddAsync(notification);
        return notification;
    }

    private async Task UpdateHistoryAfterDispatchAsync(
        Notificacion notification,
        PushGatewayResult gatewayResult)
    {
        notification.EstadoEnvio = gatewayResult.Status;
        notification.UltimoIntentoEn = DateTime.UtcNow;
        if (gatewayResult.Success)
        {
            notification.EnviadoEn = DateTime.UtcNow;
        }

        await _notificacionRepository.UpdateAsync(notification);
    }

    private async Task<List<string>> ResolvePushTokensAsync(Usuario usuario)
    {
        var dispositivos = await _dispositivoRepository.GetActiveByUsuarioIdAsync(usuario.Id);
        var tokens = dispositivos?
            .Where(d => !string.IsNullOrEmpty(d.TokenFcm))
            .Select(d => d.TokenFcm)
            .ToList() ?? [];

        if (tokens.Count == 0 && !string.IsNullOrEmpty(usuario.FcmToken))
        {
            tokens.Add(usuario.FcmToken);
        }

        return tokens;
    }

    private async Task<PushGatewayResult> SendToTokensAsync(
        List<string> tokens,
        string title,
        string message,
        IReadOnlyDictionary<string, string>? data,
        CancellationToken cancellationToken = default)
    {
        var sent = 0;
        var failed = 0;
        PushGatewayResult? lastResult = null;

        foreach (var token in tokens)
        {
            try
            {
                var result = await _pushGateway.SendAsync(token, title, message, data, cancellationToken);
                lastResult = result;
                if (result.Success)
                {
                    sent++;
                }
                else
                {
                    failed++;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                failed++;
            }
        }

        return lastResult is null
            ? new PushGatewayResult(false, "Fallido")
            : new PushGatewayResult(sent > 0, lastResult.Success ? "Enviado" : lastResult.Status);
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
            ["type"] = "Alert",
            ["event"] = alerta.Tipo == "SOS" ? "WearableSosTriggered" : "CriticalAlertCreated",
            ["entityId"] = alerta.Id.ToString(),
            ["alertId"] = alerta.Id.ToString(),
            ["alertType"] = alerta.Tipo,
            ["severity"] = alerta.Severidad,
            ["deepLink"] = $"/app/alerts?alertId={alerta.Id}",
            ["contractVersion"] = ImpactX.Core.ApiContract.ApiContractDefinition.ContractVersion,
            ["createdAt"] = alerta.CreadoEn.ToString("O"),
        };

        return (title, message, data);
    }

    private async Task<Dictionary<string, string>> EnrichAlertDataAsync(
        IReadOnlyDictionary<string, string> source,
        Notificacion notification,
        MonitoringRelationship relationship)
    {
        var result = new Dictionary<string, string>(source)
        {
            ["notificationId"] = notification.Id.ToString(),
            ["unreadCount"] = (await _notificacionRepository.CountUnreadByUserAsync(notification.UsuarioId)).ToString(),
            ["publicRelationshipId"] = relationship.PublicRelationshipId
        };
        return result;
    }

    private static void AddIfPresent(
        IDictionary<string, string> data,
        string key,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            data[key] = value;
    }

    private static string BuildIdempotencyKey(Guid alertaId, Guid recipientUserId)
    {
        return $"alert:{alertaId}:recipient:{recipientUserId}:channel:push";
    }

    private static NotificacionDto MapToDto(Notificacion n) => new()
    {
        Id = n.Id,
        Titulo = n.Titulo,
        Mensaje = n.Mensaje,
        Tipo = n.Tipo,
        ReferenciaId = n.ReferenciaId,
        ReferenciaTipo = n.ReferenciaTipo,
        PublicRelationshipId = n.PublicRelationshipId,
        Evento = n.Evento,
        DeepLink = n.DeepLink,
        EntityId = n.EntityId,
        Leida = n.Leida,
        LeidaEn = n.LeidaEn,
        CreadoEn = n.CreadoEn,
    };
}
