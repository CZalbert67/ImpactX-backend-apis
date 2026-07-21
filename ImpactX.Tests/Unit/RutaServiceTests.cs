using ImpactX.Core.Exceptions;
using Moq;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Models.DTOs;
using ImpactX.Services;

namespace ImpactX.Tests.Unit;

public class RutaServiceTests
{
    private readonly Mock<IRutaRepository> _rutaRepo;
    private readonly RutaService _rutaService;

    public RutaServiceTests()
    {
        _rutaRepo = new Mock<IRutaRepository>();
        _rutaService = new RutaService(_rutaRepo.Object);
    }

    [Fact]
    public async Task GetFrequentAsync_ReturnsFrequentRoutes()
    {
        var usuarioId = Guid.NewGuid();
        _rutaRepo.Setup(r => r.GetFrequentByUserAsync(usuarioId))
            .ReturnsAsync([new Ruta { Id = Guid.NewGuid(), UsuarioId = usuarioId, Nombre = "Casa-Trabajo", EsFrecuente = true }]);

        var result = await _rutaService.GetFrequentAsync(usuarioId);

        Assert.Single(result);
        Assert.Equal("Casa-Trabajo", result[0].Nombre);
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsHistory()
    {
        var usuarioId = Guid.NewGuid();
        _rutaRepo.Setup(r => r.GetHistoryByUserAsync(usuarioId))
            .ReturnsAsync([new Ruta { Id = Guid.NewGuid(), UsuarioId = usuarioId, Nombre = "Viaje pasado", EsFrecuente = false }]);

        var result = await _rutaService.GetHistoryAsync(usuarioId);

        Assert.Single(result);
        Assert.False(result[0].EsFrecuente);
    }

    [Fact]
    public async Task CreateAsync_CreatesRoute()
    {
        var usuarioId = Guid.NewGuid();

        var result = await _rutaService.CreateAsync(usuarioId, new CreateRutaRequest
        {
            Nombre = "Casa-Gimnasio",
            Origen = "Casa",
            OrigenLat = 19.43,
            OrigenLng = -99.13,
            Destino = "Gimnasio",
            DestinoLat = 19.45,
            DestinoLng = -99.15,
            DistanciaKm = 3.5,
            DuracionEstimadaMin = 15,
            EsFrecuente = true,
        });

        Assert.Equal("Casa-Gimnasio", result.Nombre);
        Assert.True(result.EsFrecuente);
        _rutaRepo.Verify(r => r.AddAsync(It.IsAny<Ruta>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesFields()
    {
        var usuarioId = Guid.NewGuid();
        var ruta = new Ruta { Id = Guid.NewGuid(), UsuarioId = usuarioId, Nombre = "Old" };
        _rutaRepo.Setup(r => r.GetByIdAsync(ruta.Id)).ReturnsAsync(ruta);

        var result = await _rutaService.UpdateAsync(usuarioId, ruta.Id, new UpdateRutaRequest
        {
            Nombre = "New",
            EsFrecuente = false,
        });

        Assert.Equal("New", result.Nombre);
        Assert.False(result.EsFrecuente);
        _rutaRepo.Verify(r => r.UpdateAsync(ruta), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WrongUser_Throws()
    {
        var usuarioId = Guid.NewGuid();
        var otroUsuarioId = Guid.NewGuid();
        var ruta = new Ruta { Id = Guid.NewGuid(), UsuarioId = otroUsuarioId };
        _rutaRepo.Setup(r => r.GetByIdAsync(ruta.Id)).ReturnsAsync(ruta);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _rutaService.UpdateAsync(usuarioId, ruta.Id, new UpdateRutaRequest()));
    }

    [Fact]
    public async Task DeleteAsync_Deletes()
    {
        var usuarioId = Guid.NewGuid();
        var ruta = new Ruta { Id = Guid.NewGuid(), UsuarioId = usuarioId };
        _rutaRepo.Setup(r => r.GetByIdAsync(ruta.Id)).ReturnsAsync(ruta);

        await _rutaService.DeleteAsync(usuarioId, ruta.Id);

        _rutaRepo.Verify(r => r.DeleteAsync(ruta), Times.Once);
    }

    [Fact]
    public async Task SelectTodayAsync_SetsSelectedAndUnsetsPrevious()
    {
        var usuarioId = Guid.NewGuid();
        var previous = new Ruta { Id = Guid.NewGuid(), UsuarioId = usuarioId, SeleccionadaHoy = true };
        var ruta = new Ruta { Id = Guid.NewGuid(), UsuarioId = usuarioId, SeleccionadaHoy = false };
        _rutaRepo.Setup(r => r.GetByIdAsync(ruta.Id)).ReturnsAsync(ruta);
        _rutaRepo.Setup(r => r.GetSelectedTodayAsync(usuarioId)).ReturnsAsync(previous);

        var result = await _rutaService.SelectTodayAsync(usuarioId, new SelectTodayRequest { RutaId = ruta.Id });

        Assert.True(result.SeleccionadaHoy);
        Assert.False(previous.SeleccionadaHoy);
        _rutaRepo.Verify(r => r.UpdateAsync(previous), Times.Once);
        _rutaRepo.Verify(r => r.UpdateAsync(ruta), Times.Once);
    }
}
