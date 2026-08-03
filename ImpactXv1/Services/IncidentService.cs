using System.Text;
using ImpactX.Core.Domain;
using ImpactX.Core.Exceptions;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Core.Pagination;
using ImpactX.Models.DTOs;

namespace ImpactX.Services;

public class IncidentService : IIncidentService
{
    private static readonly HashSet<string> ActiveStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "Pendiente", "Enviada", "Activa"
    };

    private readonly IIncidenteRepository _incidenteRepository;
    private readonly IPlanService _planService;
    private readonly IAlertService? _alertService;
    private readonly ILogger<IncidentService> _logger;

    public IncidentService(
        IIncidenteRepository incidenteRepository,
        IPlanService planService,
        ILogger<IncidentService> logger)
        : this(incidenteRepository, planService, null, logger)
    {
    }

    public IncidentService(
        IIncidenteRepository incidenteRepository,
        IPlanService planService,
        IAlertService? alertService,
        ILogger<IncidentService> logger)
    {
        _incidenteRepository = incidenteRepository;
        _planService = planService;
        _alertService = alertService;
        _logger = logger;
    }

    public async Task<List<IncidenteListItemDto>> GetIncidentsAsync(Guid usuarioId, IncidentFilterRequest filter)
    {
        ValidateFilter(filter);
        var incidents = await _incidenteRepository.GetFilteredAsync(
            usuarioId, filter.Severidad, filter.Desde, filter.Hasta, filter.Pagina, filter.Tamano);

        return incidents
            .Where(value => string.IsNullOrWhiteSpace(filter.Estado)
                || string.Equals(value.Estado, filter.Estado.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(MapToListDto)
            .ToList();
    }

    public async Task<List<IncidenteListItemDto>> GetActiveIncidentsAsync(Guid usuarioId)
    {
        var incidents = await _incidenteRepository.GetByUserAsync(usuarioId);
        return incidents
            .Where(value => ActiveStates.Contains(value.Estado))
            .OrderByDescending(value => value.ActualizadoEn)
            .Select(MapToListDto)
            .ToList();
    }

    public async Task<IncidenteDetailDto> GetIncidentDetailAsync(Guid usuarioId, Guid incidenteId)
    {
        var incident = await RequireIncidentAsync(usuarioId, incidenteId);
        return MapToDetailDto(incident);
    }

    public async Task<IncidentActionResponse> ConfirmOkAsync(Guid usuarioId, Guid incidenteId)
    {
        var incident = await RequireIncidentAsync(usuarioId, incidenteId);
        if (_alertService is null)
            throw new InvalidOperationException("El servicio de alertas no está disponible.");

        await _alertService.ConfirmOkAsync(usuarioId, incident.AlertaId);
        var updated = await _incidenteRepository.GetByAlertIdAsync(usuarioId, incident.AlertaId) ?? incident;
        return new IncidentActionResponse
        {
            IncidentId = updated.Id,
            AlertId = updated.AlertaId,
            Estado = updated.Estado,
            Mensaje = "El usuario confirmó estar bien; el incidente quedó registrado como falsa alarma."
        };
    }

    public async Task<IncidentActionResponse> CloseAsync(
        Guid usuarioId,
        Guid incidenteId,
        IncidentCloseRequest request)
    {
        var incident = await RequireIncidentAsync(usuarioId, incidenteId);
        if (_alertService is null)
            throw new InvalidOperationException("El servicio de alertas no está disponible.");

        var result = await _alertService.CloseAsync(usuarioId, incident.AlertaId, new CloseAlertRequest
        {
            MetodoCierre = request.MetodoCierre,
            Nota = request.Nota
        });
        var updated = await _incidenteRepository.GetByAlertIdAsync(usuarioId, incident.AlertaId) ?? incident;
        return new IncidentActionResponse
        {
            IncidentId = updated.Id,
            AlertId = result.AlertaId,
            Estado = updated.Estado,
            Mensaje = result.Mensaje
        };
    }

    public async Task MarkAsFalseAlarmAsync(Guid usuarioId, Guid incidenteId, MarkFalseAlarmRequest request)
    {
        var incident = await RequireIncidentAsync(usuarioId, incidenteId);
        var handledByAlertService = ActiveStates.Contains(incident.Estado) && _alertService is not null;

        if (handledByAlertService)
        {
            await _alertService!.ConfirmOkAsync(usuarioId, incident.AlertaId);
            incident = await _incidenteRepository.GetByAlertIdAsync(usuarioId, incident.AlertaId) ?? incident;
        }
        else
        {
            var now = DateTime.UtcNow;
            incident.Estado = "FalsaAlarma";
            incident.EsFalsaAlarma = true;
            incident.MetodoCierre = "FalsaAlarma";
            incident.ConfirmadaEn ??= now;
            incident.CerradaEn ??= now;
            incident.ActualizadoEn = now;
            incident.Timeline.Add([now.ToString("O"), "Incidente marcado como falsa alarma"]);
        }

        var hasNote = !string.IsNullOrWhiteSpace(request.Nota);
        if (hasNote)
        {
            incident.Nota = request.Nota!.Trim();
            incident.ActualizadoEn = DateTime.UtcNow;
        }

        // ConfirmOkAsync already persists active incidents. For direct or annotated
        // false alarms, persist the final state once to avoid duplicate Cosmos writes.
        if (!handledByAlertService || hasNote)
            await _incidenteRepository.UpdateAsync(incident);

        _logger.LogInformation("Incidente {IncidenteId} marcado como falsa alarma", incidenteId);
    }

    public async Task UpdateNoteAsync(Guid usuarioId, Guid incidenteId, NoteRequest request)
    {
        var incident = await RequireIncidentAsync(usuarioId, incidenteId);
        incident.Nota = request.Nota?.Trim() ?? string.Empty;
        incident.ActualizadoEn = DateTime.UtcNow;
        incident.Timeline.Add([DateTime.UtcNow.ToString("O"), "Nota del incidente actualizada"]);
        await _incidenteRepository.UpdateAsync(incident);
        _logger.LogInformation("Nota actualizada para incidente {IncidenteId}", incidenteId);
    }

    public async Task<MapDataDto> GetMapDataAsync(Guid usuarioId, Guid incidenteId)
    {
        var effective = await ResolveEffectiveAsync(usuarioId);
        if (!effective.MapHistoryEnabled)
            throw new ForbiddenException("La visualización en mapas solo está disponible en plan Premium.");

        var incident = await RequireIncidentAsync(usuarioId, incidenteId);
        return new MapDataDto
        {
            Lat = incident.Lat,
            Lng = incident.Lng,
            Lugar = incident.Lugar,
            MapsUrl = $"https://www.google.com/maps?q={incident.Lat},{incident.Lng}",
        };
    }

    public async Task<byte[]> ExportAsync(Guid usuarioId, string formato)
    {
        var effective = await ResolveEffectiveAsync(usuarioId);
        if (!effective.ExportEnabled)
            throw new ForbiddenException("La exportación de incidentes solo está disponible en plan Premium.");

        var incidents = await _incidenteRepository.GetByUserAsync(usuarioId);
        if (string.Equals(formato, "csv", StringComparison.OrdinalIgnoreCase))
        {
            var builder = new StringBuilder();
            builder.AppendLine("ID,Severidad,AlertaId,Tipo,Estado,Lat,Lng,Lugar,MetodoCierre,FalsaAlarma,CreadoEn,CerradaEn");
            foreach (var incident in incidents)
            {
                builder.AppendLine($"{incident.Id},{Csv(incident.Severidad)},{incident.AlertaId},{Csv(incident.Tipo)},{Csv(incident.Estado)},{incident.Lat},{incident.Lng},{Csv(incident.Lugar)},{Csv(incident.MetodoCierre)},{incident.EsFalsaAlarma},{incident.CreadoEn:O},{incident.CerradaEn:O}");
            }
            return Encoding.UTF8.GetBytes(builder.ToString());
        }

        var content = new StringBuilder();
        content.AppendLine("ImpactX - Exportación de Incidentes");
        content.AppendLine($"Total: {incidents.Count}");
        content.AppendLine($"Generado: {DateTime.UtcNow:O}");
        foreach (var incident in incidents)
            content.AppendLine($"- {incident.CreadoEn:O} | {incident.Tipo} | {incident.Severidad} | {incident.Estado} | {incident.Lugar ?? "Sin lugar"}");
        return Encoding.UTF8.GetBytes(content.ToString());
    }

    private async Task<EffectiveSubscriptionDto> ResolveEffectiveAsync(Guid userId)
    {
        var effective = await _planService.GetEffectiveSubscriptionAsync(userId);
        if (effective is not null)
            return effective;

        var current = await _planService.GetCurrentSubscriptionAsync(userId);
        var premium = string.Equals(current?.PlanNombre, "Premium", StringComparison.OrdinalIgnoreCase)
            || string.Equals(current?.PlanNombre, "Enterprise", StringComparison.OrdinalIgnoreCase);
        return new EffectiveSubscriptionDto
        {
            PlanNombre = current?.PlanNombre ?? "Free",
            MapHistoryEnabled = premium,
            ExportEnabled = premium
        };
    }

    private async Task<Incidente> RequireIncidentAsync(Guid userId, Guid incidentId)
    {
        var incident = await _incidenteRepository.GetByIdAsync(userId, incidentId)
            ?? throw new NotFoundException("Incidente no encontrado.");
        if (incident.UsuarioId != userId)
            throw new ForbiddenException("No tienes permiso para acceder a este incidente.");
        return incident;
    }

    private static void ValidateFilter(IncidentFilterRequest filter)
    {
        if (filter.Pagina < 1)
            throw new BadRequestException("Pagina debe ser mayor o igual a 1.");
        if (filter.Tamano < PaginationDefaults.MinPageSize || filter.Tamano > PaginationDefaults.MaxPageSize)
            throw new BadRequestException($"Tamano debe estar entre {PaginationDefaults.MinPageSize} y {PaginationDefaults.MaxPageSize}.");
    }

    private static string Csv(string? value)
        => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";

    private static IncidenteListItemDto MapToListDto(Incidente value) => new()
    {
        Id = value.Id,
        AlertaId = value.AlertaId,
        Tipo = value.Tipo,
        Severidad = value.Severidad,
        Estado = value.Estado,
        Lat = value.Lat,
        Lng = value.Lng,
        Lugar = value.Lugar,
        MetodoCierre = value.MetodoCierre,
        EsFalsaAlarma = value.EsFalsaAlarma,
        CreadoEn = value.CreadoEn,
        ActualizadoEn = value.ActualizadoEn,
        CerradaEn = value.CerradaEn,
    };

    private static IncidenteDetailDto MapToDetailDto(Incidente value) => new()
    {
        Id = value.Id,
        AlertaId = value.AlertaId,
        Tipo = value.Tipo,
        Severidad = value.Severidad,
        Estado = value.Estado,
        Fecha = value.CreadoEn.ToString("dd/MM/yyyy"),
        Hora = value.CreadoEn.ToString("HH:mm"),
        Lat = value.Lat,
        Lng = value.Lng,
        Coords = $"{value.Lat:F6},{value.Lng:F6}",
        Lugar = value.Lugar,
        GForce = value.GForce,
        Decibeles = value.Decibeles,
        FrecuenciaCardiaca = value.FrecuenciaCardiaca,
        Canal = value.Canal,
        Activacion = value.Activacion,
        TiempoRespuesta = value.TiempoRespuesta,
        EsAutomatico = value.EsAutomatico,
        ViajeId = value.ViajeId,
        SourceTelemetryEventId = value.SourceTelemetryEventId,
        DetectionLabel = value.DetectionLabel,
        RuleVersion = value.RuleVersion,
        DetectionScore = value.DetectionScore,
        MetodoCierre = value.MetodoCierre,
        EsFalsaAlarma = value.EsFalsaAlarma,
        EsBypassCritico = value.EsBypassCritico,
        EsOffline = value.EsOffline,
        Nota = value.Nota,
        Timeline = value.Timeline,
        ContactosNotificados = value.ContactosNotificados,
        CreadoEn = value.CreadoEn,
        ActualizadoEn = value.ActualizadoEn,
        EnviadaEn = value.EnviadaEn,
        ConfirmadaEn = value.ConfirmadaEn,
        CerradaEn = value.CerradaEn,
    };
}
