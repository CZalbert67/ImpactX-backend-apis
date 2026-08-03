using System.Globalization;
using ImpactX.Core.Domain;
using ImpactX.Core.ImpactDetection;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Core.Interfaces.Services;
using Microsoft.Extensions.Options;

namespace ImpactX.Services;

public sealed class ImpactAlertOrchestrator : IImpactAlertOrchestrator
{
    private static readonly IReadOnlyDictionary<string, int> SeverityRank =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["none"] = 0,
            ["bump"] = 1,
            ["moderate"] = 2,
            ["severe"] = 3,
            ["critical"] = 4
        };

    private readonly IAlertaRepository _alertRepository;
    private readonly INotificationService _notificationService;
    private readonly IIncidenteRepository? _incidentRepository;
    private readonly ImpactDetectionOptions _options;
    private readonly ILogger<ImpactAlertOrchestrator> _logger;

    public ImpactAlertOrchestrator(
        IAlertaRepository alertRepository,
        INotificationService notificationService,
        IOptions<ImpactDetectionOptions> options,
        ILogger<ImpactAlertOrchestrator> logger)
        : this(alertRepository, notificationService, null, options, logger)
    {
    }

    public ImpactAlertOrchestrator(
        IAlertaRepository alertRepository,
        INotificationService notificationService,
        IIncidenteRepository? incidentRepository,
        IOptions<ImpactDetectionOptions> options,
        ILogger<ImpactAlertOrchestrator> logger)
    {
        _alertRepository = alertRepository;
        _notificationService = notificationService;
        _incidentRepository = incidentRepository;
        _options = options.Value;
        _logger = logger;
    }

    public async Task ProcessDetectedEventsAsync(
        Guid userId,
        Viaje trip,
        IReadOnlyList<ViajeTelemetry> detectedEvents,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || detectedEvents.Count == 0)
            return;

        var candidate = detectedEvents
            .Where(value => value.ImpactCandidate == true)
            .OrderByDescending(value => Rank(value.SeverityLabel))
            .ThenByDescending(value => value.MagnitudAceleracion ?? 0d)
            .FirstOrDefault();

        if (candidate is null)
            return;

        if (await _alertRepository.GetBySourceTelemetryEventIdAsync(
                userId,
                candidate.Id,
                cancellationToken) is not null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var active = await _alertRepository.GetActiveByUserAsync(userId);
        if (active is not null
            && active.ViajeId == trip.Id.ToString()
            && now - active.CreadoEn <= TimeSpan.FromSeconds(Math.Max(1, _options.ActiveAlertCooldownSeconds)))
        {
            await PromoteExistingAlertIfNeededAsync(active, candidate, now, cancellationToken);
            return;
        }

        var immediate = candidate.SeverityLabel is "severe" or "critical";
        var alert = new Alerta
        {
            UsuarioId = userId,
            Tipo = "Impacto",
            Severidad = candidate.SeverityLabel ?? "bump",
            Estado = immediate ? "Enviada" : "Pendiente",
            Lat = candidate.Lat,
            Lng = candidate.Lng,
            GForce = ToGForce(candidate.MagnitudAceleracion),
            FrecuenciaCardiaca = candidate.FrecuenciaCardiaca?.ToString(CultureInfo.InvariantCulture),
            Modo = "auto",
            Canal = "internal",
            ViajeId = trip.Id.ToString(),
            SourceTelemetryEventId = candidate.Id,
            DetectionLabel = candidate.DetectionLabel,
            RuleVersion = candidate.RuleVersion,
            DetectionScore = candidate.DetectionScore,
            EsBypassCritico = immediate,
            CreadoEn = now,
            EnviadaEn = immediate ? now : null,
            AutoSendAtUtc = immediate
                ? null
                : now.AddSeconds(ImpactDetectionEngine.DefaultCancellationWindowSeconds),
            Timeline =
            [
                [now.ToString("O"), $"Candidato de impacto detectado: {candidate.SeverityLabel}"],
                [now.ToString("O"), $"Regla {candidate.RuleVersion}; puntaje {candidate.DetectionScore}"]
            ]
        };

        await _alertRepository.AddAsync(alert);
        await UpsertIncidentAsync(alert, cancellationToken);

        if (immediate)
        {
            await NotifySafelyAsync(alert, cancellationToken);
        }

        _logger.LogWarning(
            "Alerta automática creada para viaje {TripId}; severidad {Severity}; estado {State}",
            trip.Id,
            alert.Severidad,
            alert.Estado);
    }

    public async Task<int> DispatchDuePendingAlertsAsync(
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return 0;

        var due = await _alertRepository.GetPendingDueAsync(utcNow, 100, cancellationToken);
        var dispatched = 0;

        foreach (var alert in due)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (alert.Estado != "Pendiente" || alert.AutoSendAtUtc is null || alert.AutoSendAtUtc > utcNow)
                continue;

            alert.Estado = "Enviada";
            alert.EnviadaEn = utcNow;
            alert.Timeline.Add([utcNow.ToString("O"), "Ventana de cancelación finalizada; alerta enviada"]);
            await _alertRepository.UpdateAsync(alert);
            await UpsertIncidentAsync(alert, cancellationToken);
            await NotifySafelyAsync(alert, cancellationToken);
            dispatched++;
        }

        return dispatched;
    }

    private async Task PromoteExistingAlertIfNeededAsync(
        Alerta active,
        ViajeTelemetry candidate,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var incomingRank = Rank(candidate.SeverityLabel);
        var existingRank = Rank(active.Severidad);
        var becameImmediate = active.Estado == "Pendiente" && incomingRank >= Rank("severe");

        if (incomingRank > existingRank)
        {
            active.Severidad = candidate.SeverityLabel ?? active.Severidad;
        }

        active.Timeline.Add([
            now.ToString("O"),
            $"Nueva señal correlacionada; severidad {candidate.SeverityLabel}; puntaje {candidate.DetectionScore}"
        ]);

        if (becameImmediate)
        {
            active.Estado = "Enviada";
            active.EnviadaEn = now;
            active.AutoSendAtUtc = null;
            active.EsBypassCritico = true;
        }

        await _alertRepository.UpdateAsync(active);
        await UpsertIncidentAsync(active, cancellationToken);

        if (becameImmediate)
        {
            await NotifySafelyAsync(active, cancellationToken);
        }
    }

    private async Task UpsertIncidentAsync(Alerta alert, CancellationToken cancellationToken)
    {
        if (_incidentRepository is null)
            return;

        var incident = await _incidentRepository.GetByAlertIdAsync(alert.UsuarioId, alert.Id);
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
            await _incidentRepository.AddAsync(incident);
        else
            await _incidentRepository.UpdateAsync(incident);
    }

    private async Task NotifySafelyAsync(Alerta alert, CancellationToken cancellationToken)
    {
        try
        {
            await _notificationService.NotifyAlertMonitorsAsync(alert, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Notificación automática cancelada para alerta {AlertId}", alert.Id);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // La telemetría y la alerta ya fueron persistidas. Un fallo de FCM
            // no debe convertir la ingesta idempotente en un 500 ni provocar
            // que el wearable reenvíe indefinidamente el mismo evento.
            _logger.LogError(ex, "No fue posible notificar la alerta automática {AlertId}", alert.Id);
        }
    }

    private static int Rank(string? severity)
    {
        return severity is not null && SeverityRank.TryGetValue(severity, out var rank)
            ? rank
            : 0;
    }

    private static string? ToGForce(double? accelerationMagnitude)
    {
        return accelerationMagnitude is null
            ? null
            : (accelerationMagnitude.Value / 9.80665d).ToString("F2", CultureInfo.InvariantCulture);
    }
}
