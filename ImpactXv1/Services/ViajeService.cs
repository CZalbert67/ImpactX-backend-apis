using ImpactX.Core.Domain;
using ImpactX.Core.Exceptions;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Models.DTOs;

namespace ImpactX.Services;

public class ViajeService : IViajeService
{
    private readonly IViajeRepository _viajeRepository;
    private readonly ILogger<ViajeService> _logger;

    public ViajeService(IViajeRepository viajeRepository, ILogger<ViajeService> logger)
    {
        _viajeRepository = viajeRepository;
        _logger = logger;
    }

    public async Task<ViajeDto> StartAsync(Guid usuarioId, StartTripRequest request)
    {
        var active = await _viajeRepository.GetActiveByUserAsync(usuarioId);
        if (active is not null)
            throw new ConflictException("Ya tienes un viaje activo. Finalízalo antes de iniciar uno nuevo.");

        var viaje = new Viaje
        {
            UsuarioId = usuarioId,
            DispositivoId = request.DispositivoId,
            Estado = "Activo",
            Inicio = DateTime.UtcNow,
            Proposito = request.Proposito,
            RutaOrigen = request.RutaOrigen,
            RutaDestino = request.RutaDestino,
        };

        await _viajeRepository.AddAsync(viaje);

        _logger.LogInformation("Viaje {ViajeId} iniciado para usuario {UsuarioId}", viaje.Id, usuarioId);

        return MapToDto(viaje);
    }

    public async Task<TripActionResponse> PauseAsync(Guid usuarioId, Guid viajeId)
    {
        var viaje = await GetOwnedViajeAsync(usuarioId, viajeId);

        if (viaje.Estado != "Activo")
            throw new ConflictException("Solo se puede pausar un viaje activo.");

        viaje.Estado = "Pausado";
        await _viajeRepository.UpdateAsync(viaje);

        return new TripActionResponse { ViajeId = viajeId, Estado = "Pausado", Mensaje = "Viaje pausado." };
    }

    public async Task<TripActionResponse> ResumeAsync(Guid usuarioId, Guid viajeId)
    {
        var viaje = await GetOwnedViajeAsync(usuarioId, viajeId);

        if (viaje.Estado != "Pausado")
            throw new ConflictException("Solo se puede reanudar un viaje pausado.");

        viaje.Estado = "Activo";
        await _viajeRepository.UpdateAsync(viaje);

        return new TripActionResponse { ViajeId = viajeId, Estado = "Activo", Mensaje = "Viaje reanudado." };
    }

    public async Task<ViajeDto> FinishAsync(Guid usuarioId, Guid viajeId)
    {
        var viaje = await GetOwnedViajeAsync(usuarioId, viajeId);

        if (viaje.Estado == "Finalizado")
            throw new ConflictException("Este viaje ya fue finalizado.");

        viaje.Estado = "Finalizado";
        viaje.Fin = DateTime.UtcNow;

        if (viaje.Inicio.Kind == DateTimeKind.Utc)
            viaje.DuracionMinutos = (int)(viaje.Fin.Value - viaje.Inicio).TotalMinutes;

        var telemetry = await _viajeRepository.GetTelemetryByViajeAsync(viajeId);
        if (telemetry.Count > 0)
        {
            viaje.DistanciaRecorridaKm = CalculateDistance(telemetry);
            viaje.VelocidadPromedio = telemetry.Average(t => t.Velocidad);
            viaje.VelocidadMaxima = telemetry.Max(t => t.Velocidad);
        }

        await _viajeRepository.UpdateAsync(viaje);

        _logger.LogInformation("Viaje {ViajeId} finalizado para usuario {UsuarioId}", viajeId, usuarioId);

        return MapToDto(viaje);
    }

    public async Task<List<TelemetryPointDto>> UpdateTelemetryAsync(Guid usuarioId, Guid viajeId, TelemetryUpdateRequest request)
    {
        var viaje = await GetOwnedViajeAsync(usuarioId, viajeId);

        if (viaje.Estado != "Activo" && viaje.Estado != "Pausado")
            throw new ConflictException("Solo se puede enviar telemetría de un viaje activo o pausado.");

        foreach (var punto in request.Puntos)
        {
            var telemetry = new ViajeTelemetry
            {
                ViajeId = viajeId,
                UsuarioId = usuarioId,
                Timestamp = punto.Timestamp,
                Lat = punto.Lat,
                Lng = punto.Lng,
                Velocidad = punto.Velocidad,
                Altitud = punto.Altitud,
                Heading = punto.Heading,
            };
            await _viajeRepository.AddTelemetryAsync(telemetry);
        }

        return request.Puntos;
    }

    public async Task<ViajeDto?> GetActiveAsync(Guid usuarioId)
    {
        var viaje = await _viajeRepository.GetActiveByUserAsync(usuarioId);
        return viaje is null ? null : MapToDto(viaje);
    }

    private async Task<Viaje> GetOwnedViajeAsync(Guid usuarioId, Guid viajeId)
    {
        var viaje = await _viajeRepository.GetByIdAsync(viajeId)
            ?? throw new NotFoundException("Viaje no encontrado.");

        if (viaje.UsuarioId != usuarioId)
            throw new ForbiddenException("No tienes permiso para acceder a este viaje.");

        return viaje;
    }

    private static double CalculateDistance(List<ViajeTelemetry> points)
    {
        double totalKm = 0;
        for (int i = 1; i < points.Count; i++)
        {
            totalKm += Haversine(points[i - 1].Lat, points[i - 1].Lng, points[i].Lat, points[i].Lng);
        }
        return Math.Round(totalKm, 2);
    }

    private static double Haversine(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static ViajeDto MapToDto(Viaje v) => new()
    {
        Id = v.Id,
        DispositivoId = v.DispositivoId,
        Estado = v.Estado,
        Inicio = v.Inicio,
        Fin = v.Fin,
        DistanciaRecorridaKm = v.DistanciaRecorridaKm,
        DuracionMinutos = v.DuracionMinutos,
        VelocidadPromedio = v.VelocidadPromedio,
        VelocidadMaxima = v.VelocidadMaxima,
        RiesgoMaximo = v.RiesgoMaximo,
        Proposito = v.Proposito,
        RutaOrigen = v.RutaOrigen,
        RutaDestino = v.RutaDestino,
    };
}
