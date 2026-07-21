using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Models.DTOs;
using ImpactX.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace ImpactX.Tests.Unit;

public class AnalyticsServiceTests
{
    private readonly Mock<IIncidenteRepository> _incidenteRepo;
    private readonly Mock<IContactoRepository> _contactoRepo;
    private readonly Mock<IViajeRepository> _viajeRepo;
    private readonly Mock<ISuscripcionRepository> _suscripcionRepo;
    private readonly AnalyticsService _analyticsService;

    public AnalyticsServiceTests()
    {
        _incidenteRepo = new Mock<IIncidenteRepository>();
        _contactoRepo = new Mock<IContactoRepository>();
        _viajeRepo = new Mock<IViajeRepository>();
        _suscripcionRepo = new Mock<ISuscripcionRepository>();
        var logger = Mock.Of<ILogger<AnalyticsService>>();
        _analyticsService = new AnalyticsService(
            _incidenteRepo.Object, _contactoRepo.Object,
            _viajeRepo.Object, _suscripcionRepo.Object, logger);
    }

    [Fact]
    public async Task GetDashboardAsync_WithData_ReturnsMetrics()
    {
        var usuarioId = Guid.NewGuid();
        _incidenteRepo.Setup(r => r.CountByUserAsync(usuarioId)).ReturnsAsync(15);
        _contactoRepo.Setup(r => r.CountByUserAsync(usuarioId)).ReturnsAsync(3);
        _viajeRepo.Setup(r => r.GetByUserAsync(usuarioId)).ReturnsAsync([
            new Viaje { Id = Guid.NewGuid(), UsuarioId = usuarioId },
            new Viaje { Id = Guid.NewGuid(), UsuarioId = usuarioId },
        ]);
        _suscripcionRepo.Setup(r => r.GetActiveByUserAsync(usuarioId))
            .ReturnsAsync(new Suscripcion { Inicio = DateTime.UtcNow.AddDays(-45), Estado = "Activa" });
        _incidenteRepo.Setup(r => r.GetByUserAsync(usuarioId)).ReturnsAsync([
            new Incidente { Canal = "manual", Severidad = "crash" },
            new Incidente { Canal = "manual", Severidad = "bump" },
            new Incidente { Canal = "auto", Severidad = "severe" },
        ]);

        var result = await _analyticsService.GetDashboardAsync(usuarioId);

        Assert.Equal(15, result.TotalIncidentes);
        Assert.Equal(3, result.ContactosActivos);
        Assert.Equal(2, result.ViajesRegistrados);
        Assert.Equal(45, result.DiasDePlan);
        Assert.Equal("manual", result.CanalMasUsado);
    }

    [Fact]
    public async Task GetDashboardAsync_WithoutSubscription_ReturnsDefaults()
    {
        var usuarioId = Guid.NewGuid();
        _incidenteRepo.Setup(r => r.CountByUserAsync(usuarioId)).ReturnsAsync(0);
        _contactoRepo.Setup(r => r.CountByUserAsync(usuarioId)).ReturnsAsync(0);
        _viajeRepo.Setup(r => r.GetByUserAsync(usuarioId)).ReturnsAsync([]);
        _suscripcionRepo.Setup(r => r.GetActiveByUserAsync(usuarioId)).ReturnsAsync((Suscripcion?)null);
        _incidenteRepo.Setup(r => r.GetByUserAsync(usuarioId)).ReturnsAsync([]);

        var result = await _analyticsService.GetDashboardAsync(usuarioId);

        Assert.Equal(0, result.TotalIncidentes);
        Assert.Equal(0, result.DiasDePlan);
        Assert.Equal("Free", result.PlanActual);
        Assert.Null(result.CanalMasUsado);
    }

    [Fact]
    public async Task GetDashboardAsync_WithoutIncidents_CanalIsNull()
    {
        var usuarioId = Guid.NewGuid();
        _incidenteRepo.Setup(r => r.CountByUserAsync(usuarioId)).ReturnsAsync(0);
        _contactoRepo.Setup(r => r.CountByUserAsync(usuarioId)).ReturnsAsync(0);
        _viajeRepo.Setup(r => r.GetByUserAsync(usuarioId)).ReturnsAsync([]);
        _incidenteRepo.Setup(r => r.GetByUserAsync(usuarioId)).ReturnsAsync([]);

        var result = await _analyticsService.GetDashboardAsync(usuarioId);

        Assert.Null(result.CanalMasUsado);
    }

    [Fact]
    public async Task GetIncidentTrendAsync_WithMonthlyGrouping_ReturnsPoints()
    {
        var usuarioId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        _incidenteRepo.Setup(r => r.GetFilteredAsync(usuarioId, null, It.IsAny<DateTime>(), null, 1, int.MaxValue))
            .ReturnsAsync([
                new Incidente { Severidad = "crash", CreadoEn = now.AddDays(-5) },
                new Incidente { Severidad = "bump", CreadoEn = now.AddDays(-10) },
                new Incidente { Severidad = "severe", CreadoEn = now.AddDays(-3) },
                new Incidente { Severidad = "crash", CreadoEn = now.AddMonths(-2) },
            ]);

        var result = await _analyticsService.GetIncidentTrendAsync(usuarioId, new TrendFilterRequest
        {
            Agrupacion = "month",
            Meses = 6,
        });

        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task GetIncidentTrendAsync_WithNoIncidents_ReturnsEmpty()
    {
        var usuarioId = Guid.NewGuid();
        _incidenteRepo.Setup(r => r.GetFilteredAsync(usuarioId, null, It.IsAny<DateTime>(), null, 1, int.MaxValue))
            .ReturnsAsync([]);

        var result = await _analyticsService.GetIncidentTrendAsync(usuarioId, new TrendFilterRequest
        {
            Agrupacion = "week",
            Meses = 3,
        });

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetTripSummaryAsync_WithTrips_ReturnsSummary()
    {
        var usuarioId = Guid.NewGuid();
        _viajeRepo.Setup(r => r.GetByUserAsync(usuarioId)).ReturnsAsync([
            new Viaje
            {
                DistanciaRecorridaKm = 15.5,
                VelocidadPromedio = 45,
                VelocidadMaxima = 80,
                DuracionMinutos = 20,
                RiesgoMaximo = "Moderado",
            },
            new Viaje
            {
                DistanciaRecorridaKm = 8.2,
                VelocidadPromedio = 35,
                VelocidadMaxima = 60,
                DuracionMinutos = 15,
                RiesgoMaximo = "Moderado",
            },
            new Viaje
            {
                DistanciaRecorridaKm = 3.0,
                VelocidadPromedio = 25,
                VelocidadMaxima = 40,
                DuracionMinutos = 8,
                RiesgoMaximo = "Bajo",
            },
        ]);

        var result = await _analyticsService.GetTripSummaryAsync(usuarioId);

        Assert.Equal(3, result.TotalViajes);
        Assert.Equal(26.7, result.DistanciaTotalKm, 1);
        Assert.Equal(35, result.VelocidadPromedio, 1);
        Assert.Equal(80, result.VelocidadMaxima, 1);
        Assert.Equal(43, result.DuracionTotalMinutos);
        Assert.Equal("Moderado", result.RiesgoMasFrecuente);
    }

    [Fact]
    public async Task GetTripSummaryAsync_WithoutTrips_ReturnsEmptySummary()
    {
        var usuarioId = Guid.NewGuid();
        _viajeRepo.Setup(r => r.GetByUserAsync(usuarioId)).ReturnsAsync([]);

        var result = await _analyticsService.GetTripSummaryAsync(usuarioId);

        Assert.Equal(0, result.TotalViajes);
        Assert.Equal(0, result.DistanciaTotalKm);
        Assert.Equal(0, result.VelocidadPromedio);
        Assert.Equal(0, result.VelocidadMaxima);
        Assert.Equal(0, result.DuracionTotalMinutos);
        Assert.Null(result.RiesgoMasFrecuente);
    }

    [Fact]
    public async Task GetTripSummaryAsync_WithSingleTrip_ReturnsCorrectValues()
    {
        var usuarioId = Guid.NewGuid();
        _viajeRepo.Setup(r => r.GetByUserAsync(usuarioId)).ReturnsAsync([
            new Viaje
            {
                DistanciaRecorridaKm = 100,
                VelocidadPromedio = 60,
                VelocidadMaxima = 120,
                DuracionMinutos = 100,
                RiesgoMaximo = "Alto",
            },
        ]);

        var result = await _analyticsService.GetTripSummaryAsync(usuarioId);

        Assert.Equal(1, result.TotalViajes);
        Assert.Equal(100, result.DistanciaTotalKm);
        Assert.Equal(60, result.VelocidadPromedio);
        Assert.Equal(120, result.VelocidadMaxima);
        Assert.Equal(100, result.DuracionTotalMinutos);
        Assert.Equal("Alto", result.RiesgoMasFrecuente);
    }
}
