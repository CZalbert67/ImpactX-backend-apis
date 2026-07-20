using ImpactX.Core.Domain;
using ImpactX.Core.Exceptions;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Models.DTOs;
using ImpactX.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace ImpactX.Tests.Unit;

public class IncidentServiceTests
{
    private readonly Mock<IIncidenteRepository> _incidenteRepo;
    private readonly Mock<IPlanService> _planService;
    private readonly IncidentService _incidentService;

    public IncidentServiceTests()
    {
        _incidenteRepo = new Mock<IIncidenteRepository>();
        _planService = new Mock<IPlanService>();
        var logger = Mock.Of<ILogger<IncidentService>>();
        _incidentService = new IncidentService(_incidenteRepo.Object, _planService.Object, logger);
    }

    [Fact]
    public async Task GetIncidentsAsync_ReturnsFilteredList()
    {
        var usuarioId = Guid.NewGuid();
        var incidentes = new List<Incidente>
        {
            new() { Id = Guid.NewGuid(), UsuarioId = usuarioId, Severidad = "crash", CreadoEn = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), UsuarioId = usuarioId, Severidad = "bump", CreadoEn = DateTime.UtcNow.AddDays(-1) },
        };
        _incidenteRepo.Setup(r => r.GetFilteredAsync(usuarioId, null, null, null, 1, 20))
            .ReturnsAsync(incidentes);

        var result = await _incidentService.GetIncidentsAsync(usuarioId, new IncidentFilterRequest());

        Assert.Equal(2, result.Count);
        Assert.Contains(result, i => i.Severidad == "crash");
        Assert.Contains(result, i => i.Severidad == "bump");
    }

    [Fact]
    public async Task GetIncidentsAsync_WithSeverityFilter_Filters()
    {
        var usuarioId = Guid.NewGuid();
        _incidenteRepo.Setup(r => r.GetFilteredAsync(usuarioId, "severe", null, null, 1, 20))
            .ReturnsAsync([]);

        var result = await _incidentService.GetIncidentsAsync(usuarioId, new IncidentFilterRequest
        {
            Severidad = "severe",
        });

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetIncidentDetailAsync_ReturnsDetail()
    {
        var usuarioId = Guid.NewGuid();
        var incidente = new Incidente
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            Severidad = "crash",
            Lat = 19.43,
            Lng = -99.13,
            GForce = "5.5",
            MetodoCierre = "Atendido",
            Timeline = [["2024-01-01", "Test"]],
            ContactosNotificados = ["contacto1@test.com"],
        };
        _incidenteRepo.Setup(r => r.GetByIdAsync(incidente.Id)).ReturnsAsync(incidente);

        var result = await _incidentService.GetIncidentDetailAsync(usuarioId, incidente.Id);

        Assert.Equal("crash", result.Severidad);
        Assert.Equal(19.43, result.Lat);
        Assert.Equal("5.5", result.GForce);
        Assert.Single(result.Timeline);
        Assert.Single(result.ContactosNotificados);
    }

    [Fact]
    public async Task GetIncidentDetailAsync_WithWrongUser_Throws()
    {
        var usuarioId = Guid.NewGuid();
        var incidente = new Incidente { Id = Guid.NewGuid(), UsuarioId = Guid.NewGuid() };
        _incidenteRepo.Setup(r => r.GetByIdAsync(incidente.Id)).ReturnsAsync(incidente);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _incidentService.GetIncidentDetailAsync(usuarioId, incidente.Id));
    }

    [Fact]
    public async Task GetIncidentDetailAsync_NotFound_Throws()
    {
        _incidenteRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Incidente?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _incidentService.GetIncidentDetailAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public async Task MarkAsFalseAlarmAsync_SetsFlag()
    {
        var usuarioId = Guid.NewGuid();
        var incidente = new Incidente { Id = Guid.NewGuid(), UsuarioId = usuarioId };
        _incidenteRepo.Setup(r => r.GetByIdAsync(incidente.Id)).ReturnsAsync(incidente);

        await _incidentService.MarkAsFalseAlarmAsync(usuarioId, incidente.Id, new MarkFalseAlarmRequest
        {
            Nota = "Era un falso positivo",
        });

        Assert.True(incidente.EsFalsaAlarma);
        Assert.Equal("Era un falso positivo", incidente.Nota);
        _incidenteRepo.Verify(r => r.UpdateAsync(incidente), Times.Once);
    }

    [Fact]
    public async Task MarkAsFalseAlarmAsync_WithWrongUser_Throws()
    {
        var usuarioId = Guid.NewGuid();
        var incidente = new Incidente { Id = Guid.NewGuid(), UsuarioId = Guid.NewGuid() };
        _incidenteRepo.Setup(r => r.GetByIdAsync(incidente.Id)).ReturnsAsync(incidente);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _incidentService.MarkAsFalseAlarmAsync(usuarioId, incidente.Id, new MarkFalseAlarmRequest()));
    }

    [Fact]
    public async Task UpdateNoteAsync_UpdatesNote()
    {
        var usuarioId = Guid.NewGuid();
        var incidente = new Incidente { Id = Guid.NewGuid(), UsuarioId = usuarioId };
        _incidenteRepo.Setup(r => r.GetByIdAsync(incidente.Id)).ReturnsAsync(incidente);

        await _incidentService.UpdateNoteAsync(usuarioId, incidente.Id, new NoteRequest
        {
            Nota = "Nota actualizada",
        });

        Assert.Equal("Nota actualizada", incidente.Nota);
        _incidenteRepo.Verify(r => r.UpdateAsync(incidente), Times.Once);
    }

    [Fact]
    public async Task UpdateNoteAsync_WithWrongUser_Throws()
    {
        var usuarioId = Guid.NewGuid();
        var incidente = new Incidente { Id = Guid.NewGuid(), UsuarioId = Guid.NewGuid() };
        _incidenteRepo.Setup(r => r.GetByIdAsync(incidente.Id)).ReturnsAsync(incidente);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _incidentService.UpdateNoteAsync(usuarioId, incidente.Id, new NoteRequest { Nota = "test" }));
    }

    [Fact]
    public async Task GetMapDataAsync_WithPremiumPlan_ReturnsMapUrl()
    {
        var usuarioId = Guid.NewGuid();
        var incidente = new Incidente
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            Lat = 19.43,
            Lng = -99.13,
            Lugar = "CDMX",
        };
        _incidenteRepo.Setup(r => r.GetByIdAsync(incidente.Id)).ReturnsAsync(incidente);
        _planService.Setup(p => p.GetCurrentSubscriptionAsync(usuarioId))
            .ReturnsAsync(new SuscripcionDto { PlanNombre = "Premium" });

        var result = await _incidentService.GetMapDataAsync(usuarioId, incidente.Id);

        Assert.Equal(19.43, result.Lat);
        Assert.Equal(-99.13, result.Lng);
        Assert.Contains("google.com/maps", result.MapsUrl);
    }

    [Fact]
    public async Task GetMapDataAsync_WithFreePlan_Throws()
    {
        var usuarioId = Guid.NewGuid();
        _planService.Setup(p => p.GetCurrentSubscriptionAsync(usuarioId))
            .ReturnsAsync(new SuscripcionDto { PlanNombre = "Free" });

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _incidentService.GetMapDataAsync(usuarioId, Guid.NewGuid()));
    }

    [Fact]
    public async Task ExportAsync_WithPremiumPlan_ReturnsCsvData()
    {
        var usuarioId = Guid.NewGuid();
        _planService.Setup(p => p.GetCurrentSubscriptionAsync(usuarioId))
            .ReturnsAsync(new SuscripcionDto { PlanNombre = "Premium" });
        _incidenteRepo.Setup(r => r.GetByUserAsync(usuarioId))
            .ReturnsAsync([
                new Incidente { Id = Guid.NewGuid(), UsuarioId = usuarioId, Severidad = "crash", MetodoCierre = "Atendido" },
            ]);

        var result = await _incidentService.ExportAsync(usuarioId, "csv");

        Assert.NotNull(result);
        Assert.True(result.Length > 0);
        var content = System.Text.Encoding.UTF8.GetString(result);
        Assert.Contains("ID,Severidad", content);
        Assert.Contains("crash", content);
    }

    [Fact]
    public async Task ExportAsync_WithFreePlan_Throws()
    {
        var usuarioId = Guid.NewGuid();
        _planService.Setup(p => p.GetCurrentSubscriptionAsync(usuarioId))
            .ReturnsAsync(new SuscripcionDto { PlanNombre = "Free" });

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _incidentService.ExportAsync(usuarioId, "pdf"));
    }

    [Fact]
    public async Task ExportAsync_ExportData_ContainsHeader()
    {
        var usuarioId = Guid.NewGuid();
        _planService.Setup(p => p.GetCurrentSubscriptionAsync(usuarioId))
            .ReturnsAsync(new SuscripcionDto { PlanNombre = "Premium" });
        _incidenteRepo.Setup(r => r.GetByUserAsync(usuarioId))
            .ReturnsAsync([]);

        var result = await _incidentService.ExportAsync(usuarioId, "csv");

        var content = System.Text.Encoding.UTF8.GetString(result);
        Assert.Contains("ID,Severidad", content);
    }
}
