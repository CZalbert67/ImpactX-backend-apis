using ImpactX.Core.Domain;
using ImpactX.Core.Exceptions;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Models.DTOs;

namespace ImpactX.Services;

public class AlertService : IAlertService
{
    private readonly IAlertaRepository _alertaRepository;
    private readonly IIncidenteRepository _incidenteRepository;
    private readonly IPlanService _planService;
    private readonly ILogger<AlertService> _logger;

    public AlertService(
        IAlertaRepository alertaRepository,
        IIncidenteRepository incidenteRepository,
        IPlanService planService,
        ILogger<AlertService> logger)
    {
        _alertaRepository = alertaRepository;
        _incidenteRepository = incidenteRepository;
        _planService = planService;
        _logger = logger;
    }

    public async Task<AlertStatusDto> DetectAsync(Guid usuarioId, DetectAlertRequest request)
    {
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
            CreadoEn = DateTime.UtcNow,
            Timeline = [[DateTime.UtcNow.ToString("O"), $"Impacto detectado: {request.Severidad}"]],
        };

        await _alertaRepository.AddAsync(alerta);
        _logger.LogInformation("Impacto detectado para usuario {UsuarioId}: severidad={Severidad}, alerta={AlertaId}",
            usuarioId, request.Severidad, alerta.Id);

        return MapToDto(alerta);
    }

    public async Task<AlertStatusDto> SendSosAsync(Guid usuarioId, SosRequest request)
    {
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
        _logger.LogWarning("SOS enviado para usuario {UsuarioId}: severidad={Severidad}, canal={Canal}",
            usuarioId, request.Severidad, request.Canal);

        return MapToDto(alerta);
    }

    public async Task<ConfirmOkResponse> ConfirmOkAsync(Guid usuarioId, Guid alertaId)
    {
        var alerta = await _alertaRepository.GetByIdAsync(alertaId)
            ?? throw new NotFoundException("Alerta no encontrada.");

        if (alerta.UsuarioId != usuarioId)
            throw new ForbiddenException("No tienes permiso para modificar esta alerta.");

        if (alerta.Estado != "Pendiente" && alerta.Estado != "Enviada")
            throw new ConflictException("Esta alerta ya no está activa.");

        alerta.Estado = "FalsaAlarma";
        alerta.EsFalsaAlarma = true;
        alerta.ConfirmadaEn = DateTime.UtcNow;
        alerta.CerradaEn = DateTime.UtcNow;
        alerta.MetodoCierre = "ConfirmacionOk";
        alerta.Timeline.Add([DateTime.UtcNow.ToString("O"), "Usuario confirmó estar bien — alerta cancelada"]);

        await _alertaRepository.UpdateAsync(alerta);
        _logger.LogInformation("Alerta {AlertaId} cancelada por usuario {UsuarioId} (confirmó estar bien)", alertaId, usuarioId);

        return new ConfirmOkResponse
        {
            Mensaje = "Alerta cancelada. No se envió notificación a emergencias.",
            EsFalsaAlarma = true,
        };
    }

    public async Task<AlertActionResponse> BypassCriticalAsync(Guid usuarioId, Guid alertaId)
    {
        var alerta = await _alertaRepository.GetByIdAsync(alertaId)
            ?? throw new NotFoundException("Alerta no encontrada.");

        if (alerta.UsuarioId != usuarioId)
            throw new ForbiddenException("No tienes permiso para modificar esta alerta.");

        if (alerta.Estado == "Cerrada" || alerta.Estado == "Atendida" || alerta.Estado == "FalsaAlarma")
            throw new ConflictException("Esta alerta ya fue cerrada.");

        alerta.EsBypassCritico = true;
        alerta.Estado = "Activa";
        alerta.Timeline.Add([DateTime.UtcNow.ToString("O"), "Bypass crítico activado — alerta inmediata"]);

        await _alertaRepository.UpdateAsync(alerta);
        _logger.LogCritical("BYPASS CRÍTICO para alerta {AlertaId} del usuario {UsuarioId}", alertaId, usuarioId);

        return new AlertActionResponse
        {
            AlertaId = alerta.Id,
            Estado = alerta.Estado,
            Mensaje = "Bypass crítico activado. Contactos de emergencia notificados.",
        };
    }

    public async Task<AlertActionResponse> RetryAsync(Guid usuarioId, Guid alertaId)
    {
        var alerta = await _alertaRepository.GetByIdAsync(alertaId)
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

        return new AlertActionResponse
        {
            AlertaId = alerta.Id,
            Estado = alerta.Estado,
            Mensaje = $"Reintento #{alerta.Reintentos} enviado.",
        };
    }

    public async Task<AlertStatusDto> GetStatusAsync(Guid usuarioId, Guid alertaId)
    {
        var alerta = await _alertaRepository.GetByIdAsync(alertaId)
            ?? throw new NotFoundException("Alerta no encontrada.");

        if (alerta.UsuarioId != usuarioId)
            throw new ForbiddenException("No tienes permiso para ver esta alerta.");

        return MapToDto(alerta);
    }

    public async Task<AlertActionResponse> CloseAsync(Guid usuarioId, Guid alertaId, CloseAlertRequest request)
    {
        var alerta = await _alertaRepository.GetByIdAsync(alertaId)
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

        var incidente = new Incidente
        {
            UsuarioId = usuarioId,
            AlertaId = alerta.Id,
            Severidad = alerta.Severidad,
            Lat = alerta.Lat,
            Lng = alerta.Lng,
            Lugar = alerta.Lugar,
            GForce = alerta.GForce,
            Decibeles = alerta.Decibeles,
            FrecuenciaCardiaca = alerta.FrecuenciaCardiaca,
            Canal = alerta.Canal,
            MetodoCierre = metodoCierre,
            EsFalsaAlarma = alerta.EsFalsaAlarma,
            EsBypassCritico = alerta.EsBypassCritico,
            EsOffline = alerta.EsOffline,
            Nota = alerta.Nota,
            Timeline = alerta.Timeline,
            ContactosNotificados = alerta.ContactosNotificados,
            CreadoEn = alerta.CreadoEn,
            CerradaEn = alerta.CerradaEn,
        };

        await _incidenteRepository.AddAsync(incidente);
        _logger.LogInformation("Alerta {AlertaId} cerrada como {MetodoCierre}, incidente {IncidenteId} creado",
            alertaId, metodoCierre, incidente.Id);

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
            resultados.Add(MapToDto(alerta));
        }

        _logger.LogInformation("{Count} alertas offline sincronizadas para usuario {UsuarioId}",
            request.Alertas.Count, usuarioId);

        return resultados;
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
}
