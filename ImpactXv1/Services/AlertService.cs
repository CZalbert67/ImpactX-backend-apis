using ImpactX.Core.Domain;
using ImpactX.Core.Exceptions;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Core.Pagination;
using ImpactX.Models.DTOs;
using Microsoft.Extensions.Logging;

namespace ImpactX.Services;

public class AlertService : IAlertService
{
    private readonly IAlertaRepository _alertaRepository;
    private readonly IIncidenteRepository _incidenteRepository;
    private readonly IPlanService _planService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<AlertService> _logger;

    public AlertService(
        IAlertaRepository alertaRepository,
        IIncidenteRepository incidenteRepository,
        IPlanService planService,
        INotificationService notificationService,
        ILogger<AlertService> logger)
    {
        _alertaRepository = alertaRepository;
        _incidenteRepository = incidenteRepository;
        _planService = planService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<AlertStatusDto> DetectAsync(Guid usuarioId, DetectAlertRequest request)
    {
        if (request.ClientEventId.HasValue)
        {
            var existing = await _alertaRepository.GetBySourceTelemetryEventIdAsync(
                usuarioId,
                request.ClientEventId.Value);
            if (existing is not null)
                return MapToDto(existing);
        }

        var alerta = new Alerta
        {
            UsuarioId = usuarioId,
            Tipo = "Impacto",
            Severidad = request.Severidad,
            Estado = "Pendiente",
            Lat = request.Lat,
            Lng = request.Lng,
            Lugar = request.Lugar,
            GForce = request.GForce.ToString("F2"),
            Decibeles = request.Decibeles.ToString("F1"),
            FrecuenciaCardiaca = request.FrecuenciaCardiaca.ToString("F0"),
            Modo = "auto",
            ViajeId = request.ViajeId,
            SourceTelemetryEventId = request.ClientEventId,
            CreadoEn = DateTime.UtcNow,
            Timeline = [[DateTime.UtcNow.ToString("O"), $"Impacto detectado: {request.Severidad}"]],
        };

        await _alertaRepository.AddAsync(alerta);
        _logger.LogInformation("Impacto detectado para usuario {UsuarioId}, alerta {AlertaId} creada", usuarioId, alerta.Id);

        await NotifyIfEnviadaAsync(alerta);

        return MapToDto(alerta);
    }

    public async Task<AlertStatusDto> SendSosAsync(Guid usuarioId, SosRequest request)
    {
        if (request.ClientEventId.HasValue)
        {
            var existing = await _alertaRepository.GetBySourceTelemetryEventIdAsync(
                usuarioId,
                request.ClientEventId.Value);
            if (existing is not null)
            {
                _logger.LogInformation(
                    "Reintento SOS idempotente detectado para usuario {UsuarioId}, evento {ClientEventId}",
                    usuarioId,
                    request.ClientEventId);
                return MapToDto(existing);
            }
        }

        var alerta = new Alerta
        {
            UsuarioId = usuarioId,
            Tipo = "SOS",
            Severidad = request.Severidad,
            Estado = "Enviada",
            Lat = request.Lat,
            Lng = request.Lng,
            Lugar = request.Lugar,
            GForce = request.GForce,
            Decibeles = request.Decibeles,
            FrecuenciaCardiaca = request.FrecuenciaCardiaca,
            Canal = request.Canal,
            Modo = request.Modo,
            ViajeId = request.ViajeId,
            SourceTelemetryEventId = request.ClientEventId,
            CreadoEn = DateTime.UtcNow,
            EnviadaEn = DateTime.UtcNow,
            Timeline = [[DateTime.UtcNow.ToString("O"), $"SOS {request.Modo}: {request.Severidad}"]],
        };

        if (request.Modo == "immediate" || request.Severidad == "severe")
        {
            alerta.EsBypassCritico = true;
            alerta.Timeline.Add([DateTime.UtcNow.ToString("O"), "Bypass crítico activado"]);
        }

        await _alertaRepository.AddAsync(alerta);
        _logger.LogWarning("SOS enviado para usuario {UsuarioId}, alerta {AlertaId} creada", usuarioId, alerta.Id);

        await NotifyIfEnviadaAsync(alerta);
        await UpsertIncidentAsync(alerta);

        return MapToDto(alerta);
    }

    public async Task<ConfirmOkResponse> ConfirmOkAsync(Guid usuarioId, Guid alertaId)
    {
        var alerta = await _alertaRepository.GetByIdAsync(usuarioId, alertaId)
            ?? throw new NotFoundException("Alerta no encontrada.");

        if (alerta.UsuarioId != usuarioId)
            throw new ForbiddenException("No tienes permiso para modificar esta alerta.");

        if (alerta.Estado != "Pendiente" && alerta.Estado != "Enviada")
            throw new ConflictException("Esta alerta ya no está activa.");

        alerta.Estado = "FalsaAlarma";
        alerta.EsFalsaAlarma = true;
        alerta.ConfirmadaEn = DateTime.UtcNow;
        alerta.CerradaEn = DateTime.UtcNow;
        alerta.AutoSendAtUtc = null;
        alerta.MetodoCierre = "ConfirmacionOk";
        alerta.Timeline.Add([DateTime.UtcNow.ToString("O"), "Usuario confirmó estar bien — alerta cancelada"]);

        await _alertaRepository.UpdateAsync(alerta);
        await UpsertIncidentAsync(alerta);
        _logger.LogInformation("Alerta {AlertaId} cancelada por usuario {UsuarioId} (confirmó estar bien)", alertaId, usuarioId);

        return new ConfirmOkResponse
        {
            Mensaje = "Alerta cancelada. No se envió notificación a emergencias.",
            EsFalsaAlarma = true,
        };
    }

    public async Task<AlertActionResponse> BypassCriticalAsync(Guid usuarioId, Guid alertaId)
    {
        var alerta = await _alertaRepository.GetByIdAsync(usuarioId, alertaId)
            ?? throw new NotFoundException("Alerta no encontrada.");

        if (alerta.UsuarioId != usuarioId)
            throw new ForbiddenException("No tienes permiso para modificar esta alerta.");

        if (alerta.Estado == "Cerrada" || alerta.Estado == "Atendida" || alerta.Estado == "FalsaAlarma")
            throw new ConflictException("Esta alerta ya fue cerrada.");

        alerta.EsBypassCritico = true;
        alerta.Estado = "Enviada";
        alerta.EnviadaEn ??= DateTime.UtcNow;
        alerta.AutoSendAtUtc = null;
        alerta.Timeline.Add([DateTime.UtcNow.ToString("O"), "Bypass crítico activado — alerta inmediata"]);

        await _alertaRepository.UpdateAsync(alerta);
        _logger.LogCritical("BYPASS CRÍTICO para alerta {AlertaId} del usuario {UsuarioId}", alertaId, usuarioId);

        await NotifyIfEnviadaAsync(alerta);
        await UpsertIncidentAsync(alerta);

        return new AlertActionResponse
        {
            AlertaId = alerta.Id,
            Estado = alerta.Estado,
            Mensaje = "Bypass crítico activado. Contactos de emergencia notificados.",
        };
    }

    public async Task<AlertActionResponse> RetryAsync(Guid usuarioId, Guid alertaId)
    {
        var alerta = await _alertaRepository.GetByIdAsync(usuarioId, alertaId)
            ?? throw new NotFoundException("Alerta no encontrada.");

        if (alerta.UsuarioId != usuarioId)
            throw new ForbiddenException("No tienes permiso para modificar esta alerta.");

        if (alerta.Estado != "Pendiente")
            throw new ConflictException("Solo se pueden reintentar alertas pendientes.");

        alerta.Estado = "Enviada";
        alerta.EnviadaEn = DateTime.UtcNow;
        alerta.Reintentos = (alerta.Reintentos ?? 0) + 1;
        alerta.Timeline.Add([DateTime.UtcNow.ToString("O"), $"Reintento #{alerta.Reintentos}"]);

        await _alertaRepository.UpdateAsync(alerta);
        _logger.LogInformation("Reintento #{Reintentos} de alerta {AlertaId} para usuario {UsuarioId}",
            alerta.Reintentos, alertaId, usuarioId);

        await _notificationService.RetryAlertNotificationsAsync(alerta);
        await UpsertIncidentAsync(alerta);

        return new AlertActionResponse
        {
            AlertaId = alerta.Id,
            Estado = alerta.Estado,
            Mensaje = $"Reintento #{alerta.Reintentos} enviado.",
        };
    }

    public async Task<AlertStatusDto> GetStatusAsync(Guid usuarioId, Guid alertaId)
    {
        var alerta = await _alertaRepository.GetByIdAsync(usuarioId, alertaId)
            ?? throw new NotFoundException("Alerta no encontrada.");

        if (alerta.UsuarioId != usuarioId)
            throw new ForbiddenException("No tienes permiso para ver esta alerta.");

        return MapToDto(alerta);
    }

    public async Task<AlertActionResponse> CloseAsync(Guid usuarioId, Guid alertaId, CloseAlertRequest request)
    {
        var alerta = await _alertaRepository.GetByIdAsync(usuarioId, alertaId)
            ?? throw new NotFoundException("Alerta no encontrada.");

        if (alerta.UsuarioId != usuarioId)
            throw new ForbiddenException("No tienes permiso para cerrar esta alerta.");

        if (alerta.Estado == "Cerrada" || alerta.Estado == "Atendida" || alerta.Estado == "FalsaAlarma")
            throw new ConflictException("Esta alerta ya fue cerrada.");

        var metodoCierre = request.MetodoCierre;
        if (metodoCierre != "Atendido" && metodoCierre != "FalsaAlarma" && metodoCierre != "Prueba")
            throw new BadRequestException("Método de cierre inválido. Use: Atendido, FalsaAlarma o Prueba.");

        alerta.Estado = metodoCierre == "FalsaAlarma" ? "FalsaAlarma" : "Cerrada";
        alerta.CerradaEn = DateTime.UtcNow;
        alerta.MetodoCierre = metodoCierre;
        alerta.Nota = request.Nota ?? alerta.Nota;
        alerta.EsFalsaAlarma = metodoCierre == "FalsaAlarma";
        alerta.Timeline.Add([DateTime.UtcNow.ToString("O"), $"Alerta cerrada: {metodoCierre}"]);

        await _alertaRepository.UpdateAsync(alerta);

        var incidente = await UpsertIncidentAsync(alerta);
        _logger.LogInformation("Alerta {AlertaId} cerrada, incidente {IncidenteId} actualizado", alertaId, incidente.Id);

        return new AlertActionResponse
        {
            AlertaId = alerta.Id,
            Estado = alerta.Estado,
            Mensaje = $"Alerta cerrada como {metodoCierre}. Incidente registrado.",
        };
    }

    public async Task<List<AlertStatusDto>> SyncOfflineAsync(Guid usuarioId, SyncOfflineRequest request)
    {
        var resultados = new List<AlertStatusDto>();

        foreach (var offline in request.Alertas)
        {
            var alerta = new Alerta
            {
                UsuarioId = usuarioId,
                Tipo = offline.Tipo,
                Severidad = offline.Severidad,
                Estado = "Enviada",
                Lat = offline.Lat,
                Lng = offline.Lng,
                Lugar = offline.Lugar,
                GForce = offline.GForce,
                Decibeles = offline.Decibeles,
                FrecuenciaCardiaca = offline.FrecuenciaCardiaca,
                Modo = "auto",
                EsOffline = true,
                CreadoEn = offline.CreadoEn,
                EnviadaEn = DateTime.UtcNow,
                Timeline = [[offline.CreadoEn.ToString("O"), "Alerta generada offline"],
                           [DateTime.UtcNow.ToString("O"), "Sincronizada al volver conexión"]],
            };

            await _alertaRepository.AddAsync(alerta);
            await UpsertIncidentAsync(alerta);
            resultados.Add(MapToDto(alerta));
        }

        _logger.LogInformation("{Count} alertas offline sincronizadas para usuario {UsuarioId}",
            request.Alertas.Count, usuarioId);

        foreach (var alertaEntity in resultados.Select(a => new Alerta
        {
            Id = a.Id,
            UsuarioId = usuarioId,
            Tipo = a.Tipo,
            Severidad = a.Severidad,
            Estado = a.Estado,
            CreadoEn = a.CreadoEn,
        }))
        {
            await NotifyIfEnviadaAsync(alertaEntity);
        }

        return resultados;
    }

    public async Task<PagedResult<AlertStatusDto>> GetAlertsPagedAsync(Guid usuarioId, int? pageSize, string? continuationToken)
    {
        var size = PaginationValidator.Resolve(pageSize, continuationToken);
        var page = await _alertaRepository.GetByUserPagedAsync(usuarioId, size, continuationToken);
        return new PagedResult<AlertStatusDto>
        {
            Items = page.Items.Select(MapToDto).ToList(),
            ContinuationToken = page.ContinuationToken,
            HasMoreResults = page.HasMoreResults,
            PageSize = page.PageSize,
        };
    }

    private async Task<Incidente> UpsertIncidentAsync(Alerta alert)
    {
        var incident = await _incidenteRepository.GetByAlertIdAsync(alert.UsuarioId, alert.Id);
        var isNew = incident is null;
        incident ??= new Incidente
        {
            UsuarioId = alert.UsuarioId,
            AlertaId = alert.Id,
            CreadoEn = alert.CreadoEn
        };

        incident.Tipo = alert.Tipo;
        incident.Severidad = alert.Severidad;
        incident.Estado = alert.Estado;
        incident.Lat = alert.Lat;
        incident.Lng = alert.Lng;
        incident.Lugar = alert.Lugar;
        incident.GForce = alert.GForce;
        incident.Decibeles = alert.Decibeles;
        incident.FrecuenciaCardiaca = alert.FrecuenciaCardiaca;
        incident.Canal = alert.Canal;
        incident.ViajeId = alert.ViajeId;
        incident.SourceTelemetryEventId = alert.SourceTelemetryEventId;
        incident.DetectionLabel = alert.DetectionLabel;
        incident.RuleVersion = alert.RuleVersion;
        incident.DetectionScore = alert.DetectionScore;
        incident.MetodoCierre = alert.MetodoCierre ?? string.Empty;
        incident.EsFalsaAlarma = alert.EsFalsaAlarma;
        incident.EsBypassCritico = alert.EsBypassCritico;
        incident.EsOffline = alert.EsOffline;
        incident.Nota = alert.Nota;
        incident.Timeline = alert.Timeline.Select(value => value.ToArray()).ToList();
        incident.ContactosNotificados = alert.ContactosNotificados.ToList();
        incident.EnviadaEn = alert.EnviadaEn;
        incident.ConfirmadaEn = alert.ConfirmadaEn;
        incident.CerradaEn = alert.CerradaEn;
        incident.ActualizadoEn = DateTime.UtcNow;

        if (isNew)
            await _incidenteRepository.AddAsync(incident);
        else
            await _incidenteRepository.UpdateAsync(incident);

        return incident;
    }

    private async Task NotifyIfEnviadaAsync(Alerta alerta)
    {
        if (alerta.Estado != "Enviada")
            return;

        try
        {
            await _notificationService.NotifyAlertMonitorsAsync(alerta);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Notificación de monitores cancelada para alerta {AlertaId}. La alerta no se ve afectada.", alerta.Id);
        }
        catch (InvalidOperationException)
        {
            _logger.LogError("Error operacional al notificar monitores para alerta {AlertaId}. La alerta no se ve afectada.", alerta.Id);
        }
    }

    private static AlertStatusDto MapToDto(Alerta a) => new()
    {
        Id = a.Id,
        Tipo = a.Tipo,
        Severidad = a.Severidad,
        Estado = a.Estado,
        Lat = a.Lat,
        Lng = a.Lng,
        Lugar = a.Lugar,
        GForce = a.GForce,
        Decibeles = a.Decibeles,
        FrecuenciaCardiaca = a.FrecuenciaCardiaca,
        Modo = a.Modo,
        Canal = a.Canal,
        ViajeId = a.ViajeId,
        SourceTelemetryEventId = a.SourceTelemetryEventId,
        DetectionLabel = a.DetectionLabel,
        RuleVersion = a.RuleVersion,
        DetectionScore = a.DetectionScore,
        AutoSendAtUtc = a.AutoSendAtUtc,
        CancellationSecondsRemaining = ResolveCancellationSecondsRemaining(a),
        EsBypassCritico = a.EsBypassCritico,
        EsOffline = a.EsOffline,
        TiempoRespuesta = a.TiempoRespuesta,
        CreadoEn = a.CreadoEn,
        EnviadaEn = a.EnviadaEn,
        ConfirmadaEn = a.ConfirmadaEn,
        CerradaEn = a.CerradaEn,
        MetodoCierre = a.MetodoCierre,
        Nota = a.Nota,
        Timeline = a.Timeline,
        ContactosNotificados = a.ContactosNotificados,
    };

    private static int? ResolveCancellationSecondsRemaining(Alerta alert)
    {
        if (alert.Estado != "Pendiente" || alert.AutoSendAtUtc is null)
            return null;

        return Math.Max(0, (int)Math.Ceiling((alert.AutoSendAtUtc.Value - DateTime.UtcNow).TotalSeconds));
    }
}
