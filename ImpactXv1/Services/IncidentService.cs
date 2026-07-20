using System.Text;
using ImpactX.Core.Exceptions;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Models.DTOs;

namespace ImpactX.Services;

public class IncidentService : IIncidentService
{
    private readonly IIncidenteRepository _incidenteRepository;
    private readonly IPlanService _planService;
    private readonly ILogger<IncidentService> _logger;

    public IncidentService(
        IIncidenteRepository incidenteRepository,
        IPlanService planService,
        ILogger<IncidentService> logger)
    {
        _incidenteRepository = incidenteRepository;
        _planService = planService;
        _logger = logger;
    }

    public async Task<List<IncidenteListItemDto>> GetIncidentsAsync(Guid usuarioId, IncidentFilterRequest filter)
    {
        var incidentes = await _incidenteRepository.GetFilteredAsync(
            usuarioId, filter.Severidad, filter.Desde, filter.Hasta, filter.Pagina, filter.Tamano);

        return incidentes.Select(MapToListDto).ToList();
    }

    public async Task<IncidenteDetailDto> GetIncidentDetailAsync(Guid usuarioId, Guid incidenteId)
    {
        var incidente = await _incidenteRepository.GetByIdAsync(incidenteId)
            ?? throw new NotFoundException("Incidente no encontrado.");

        if (incidente.UsuarioId != usuarioId)
            throw new ForbiddenException("No tienes permiso para ver este incidente.");

        return new IncidenteDetailDto
        {
            Id = incidente.Id,
            Severidad = incidente.Severidad,
            Lat = incidente.Lat,
            Lng = incidente.Lng,
            Lugar = incidente.Lugar,
            GForce = incidente.GForce,
            Decibeles = incidente.Decibeles,
            FrecuenciaCardiaca = incidente.FrecuenciaCardiaca,
            Canal = incidente.Canal,
            MetodoCierre = incidente.MetodoCierre,
            EsFalsaAlarma = incidente.EsFalsaAlarma,
            EsBypassCritico = incidente.EsBypassCritico,
            Nota = incidente.Nota,
            Timeline = incidente.Timeline,
            ContactosNotificados = incidente.ContactosNotificados,
            CreadoEn = incidente.CreadoEn,
            CerradaEn = incidente.CerradaEn,
        };
    }

    public async Task MarkAsFalseAlarmAsync(Guid usuarioId, Guid incidenteId, MarkFalseAlarmRequest request)
    {
        var incidente = await _incidenteRepository.GetByIdAsync(incidenteId)
            ?? throw new NotFoundException("Incidente no encontrado.");

        if (incidente.UsuarioId != usuarioId)
            throw new ForbiddenException("No tienes permiso para modificar este incidente.");

        incidente.EsFalsaAlarma = true;
        if (!string.IsNullOrWhiteSpace(request.Nota))
            incidente.Nota = request.Nota;

        await _incidenteRepository.UpdateAsync(incidente);
        _logger.LogInformation("Incidente {IncidenteId} marcado como falsa alarma", incidenteId);
    }

    public async Task UpdateNoteAsync(Guid usuarioId, Guid incidenteId, NoteRequest request)
    {
        var incidente = await _incidenteRepository.GetByIdAsync(incidenteId)
            ?? throw new NotFoundException("Incidente no encontrado.");

        if (incidente.UsuarioId != usuarioId)
            throw new ForbiddenException("No tienes permiso para modificar este incidente.");

        incidente.Nota = request.Nota;
        await _incidenteRepository.UpdateAsync(incidente);
        _logger.LogInformation("Nota actualizada para incidente {IncidenteId}", incidenteId);
    }

    public async Task<MapDataDto> GetMapDataAsync(Guid usuarioId, Guid incidenteId)
    {
        var suscripcion = await _planService.GetCurrentSubscriptionAsync(usuarioId);
        var planName = suscripcion?.PlanNombre ?? "Free";

        if (planName != "Premium" && planName != "Enterprise")
            throw new ForbiddenException("La visualización en mapas solo está disponible en plan Premium.");

        var incidente = await _incidenteRepository.GetByIdAsync(incidenteId)
            ?? throw new NotFoundException("Incidente no encontrado.");

        if (incidente.UsuarioId != usuarioId)
            throw new ForbiddenException("No tienes permiso para ver este incidente.");

        var mapsUrl = $"https://www.google.com/maps?q={incidente.Lat},{incidente.Lng}";

        return new MapDataDto
        {
            Lat = incidente.Lat,
            Lng = incidente.Lng,
            Lugar = incidente.Lugar,
            MapsUrl = mapsUrl,
        };
    }

    public async Task<byte[]> ExportAsync(Guid usuarioId, string formato)
    {
        var suscripcion = await _planService.GetCurrentSubscriptionAsync(usuarioId);
        var planName = suscripcion?.PlanNombre ?? "Free";

        if (planName != "Premium" && planName != "Enterprise")
            throw new ForbiddenException("La exportación de incidentes solo está disponible en plan Premium.");

        var incidentes = await _incidenteRepository.GetByUserAsync(usuarioId);

        if (formato.ToLower() == "csv")
        {
            var sb = new StringBuilder();
            sb.AppendLine("ID,Severidad,Lat,Lng,Lugar,MetodoCierre,FalsaAlarma,CreadoEn,CerradaEn");
            foreach (var i in incidentes)
            {
                sb.AppendLine($"{i.Id},{i.Severidad},{i.Lat},{i.Lng},{i.Lugar},{i.MetodoCierre},{i.EsFalsaAlarma},{i.CreadoEn:O},{i.CerradaEn:O}");
            }
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        var pdfContent = $"ImpactX - Exportación de Incidentes\n\n" +
                         $"Total de incidentes: {incidentes.Count}\n" +
                         $"Generado: {DateTime.UtcNow:O}\n\n";

        foreach (var i in incidentes)
        {
            pdfContent += $"- {i.CreadoEn:dd/MM/yyyy HH:mm} | {i.Severidad} | {i.Lugar ?? "Sin lugar"} | {i.MetodoCierre}\n";
        }

        return Encoding.UTF8.GetBytes(pdfContent);
    }

    private static IncidenteListItemDto MapToListDto(Core.Domain.Incidente i) => new()
    {
        Id = i.Id,
        Severidad = i.Severidad,
        Lat = i.Lat,
        Lng = i.Lng,
        Lugar = i.Lugar,
        MetodoCierre = i.MetodoCierre,
        EsFalsaAlarma = i.EsFalsaAlarma,
        CreadoEn = i.CreadoEn,
        CerradaEn = i.CerradaEn,
    };
}
