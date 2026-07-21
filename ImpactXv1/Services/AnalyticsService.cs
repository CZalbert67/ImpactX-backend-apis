using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Models.DTOs;

namespace ImpactX.Services;

public class AnalyticsService : IAnalyticsService
{
    private readonly IIncidenteRepository _incidenteRepository;
    private readonly IContactoRepository _contactoRepository;
    private readonly IViajeRepository _viajeRepository;
    private readonly ISuscripcionRepository _suscripcionRepository;
    private readonly ILogger<AnalyticsService> _logger;

    public AnalyticsService(
        IIncidenteRepository incidenteRepository,
        IContactoRepository contactoRepository,
        IViajeRepository viajeRepository,
        ISuscripcionRepository suscripcionRepository,
        ILogger<AnalyticsService> logger)
    {
        _incidenteRepository = incidenteRepository;
        _contactoRepository = contactoRepository;
        _viajeRepository = viajeRepository;
        _suscripcionRepository = suscripcionRepository;
        _logger = logger;
    }

    public async Task<DashboardDto> GetDashboardAsync(Guid usuarioId)
    {
        var totalIncidentes = await _incidenteRepository.CountByUserAsync(usuarioId);
        var contactosActivos = await _contactoRepository.CountByUserAsync(usuarioId);
        var viajes = await _viajeRepository.GetByUserAsync(usuarioId);
        var suscripcion = await _suscripcionRepository.GetActiveByUserAsync(usuarioId);
        var incidentes = await _incidenteRepository.GetByUserAsync(usuarioId);

        var diasPlan = 0;
        var planActual = "Free";

        if (suscripcion != null)
        {
            planActual = "Activa";
            diasPlan = (DateTime.UtcNow - suscripcion.Inicio).Days;
        }

        var canalMasUsado = incidentes
            .Where(i => !string.IsNullOrEmpty(i.Canal))
            .GroupBy(i => i.Canal!)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();

        _logger.LogInformation("Dashboard generado para usuario {UsuarioId}: {Incidentes} incidentes, {Viajes} viajes",
            usuarioId, totalIncidentes, viajes.Count);

        return new DashboardDto
        {
            TotalIncidentes = totalIncidentes,
            ContactosActivos = contactosActivos,
            ViajesRegistrados = viajes.Count,
            DiasDePlan = diasPlan,
            PlanActual = planActual,
            CanalMasUsado = canalMasUsado,
        };
    }

    public async Task<List<IncidentTrendPointDto>> GetIncidentTrendAsync(Guid usuarioId, TrendFilterRequest filter)
    {
        var desde = DateTime.UtcNow.AddMonths(-filter.Meses);
        var incidentes = await _incidenteRepository.GetFilteredAsync(
            usuarioId, null, desde, null, 1, int.MaxValue);

        var grouped = filter.Agrupacion.ToLower() switch
        {
            "month" => incidentes
                .GroupBy(i => new { i.CreadoEn.Year, i.CreadoEn.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g => new IncidentTrendPointDto
                {
                    Periodo = $"{g.Key.Year}-{g.Key.Month:D2}",
                    Total = g.Count(),
                    Bumps = g.Count(i => i.Severidad == "bump"),
                    Crashes = g.Count(i => i.Severidad == "crash"),
                    Severe = g.Count(i => i.Severidad == "severe"),
                })
                .ToList(),
            _ => incidentes
                .GroupBy(i => new { i.CreadoEn.Year, Week = GetWeekNumber(i.CreadoEn) })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Week)
                .Select(g => new IncidentTrendPointDto
                {
                    Periodo = $"{g.Key.Year}-W{g.Key.Week:D2}",
                    Total = g.Count(),
                    Bumps = g.Count(i => i.Severidad == "bump"),
                    Crashes = g.Count(i => i.Severidad == "crash"),
                    Severe = g.Count(i => i.Severidad == "severe"),
                })
                .ToList(),
        };

        _logger.LogInformation("Tendencia de incidentes generada para usuario {UsuarioId}: {Puntos} puntos en {Meses} meses",
            usuarioId, grouped.Count, filter.Meses);

        return grouped;
    }

    public async Task<TripSummaryDto> GetTripSummaryAsync(Guid usuarioId)
    {
        var viajes = await _viajeRepository.GetByUserAsync(usuarioId);

        if (viajes.Count == 0)
        {
            return new TripSummaryDto();
        }

        var distanciaTotal = viajes.Sum(v => v.DistanciaRecorridaKm ?? 0);
        var velocidades = viajes.Where(v => v.VelocidadPromedio.HasValue).Select(v => v.VelocidadPromedio!.Value).ToList();
        var velocidadMax = viajes.Where(v => v.VelocidadMaxima.HasValue).Select(v => v.VelocidadMaxima!.Value).DefaultIfEmpty().Max();
        var duracionTotal = viajes.Sum(v => v.DuracionMinutos ?? 0);

        var riesgoMasFrecuente = viajes
            .Where(v => !string.IsNullOrEmpty(v.RiesgoMaximo))
            .GroupBy(v => v.RiesgoMaximo!)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();

        _logger.LogInformation("Resumen de viajes generado para usuario {UsuarioId}: {Distancia} km, {Viajes} viajes",
            usuarioId, distanciaTotal, viajes.Count);

        return new TripSummaryDto
        {
            TotalViajes = viajes.Count,
            DistanciaTotalKm = Math.Round(distanciaTotal, 2),
            VelocidadPromedio = velocidades.Count > 0 ? Math.Round(velocidades.Average(), 1) : 0,
            VelocidadMaxima = Math.Round(velocidadMax, 1),
            RiesgoMasFrecuente = riesgoMasFrecuente,
            DuracionTotalMinutos = duracionTotal,
        };
    }

    private static int GetWeekNumber(DateTime date)
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        return culture.Calendar.GetWeekOfYear(date, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
    }
}
