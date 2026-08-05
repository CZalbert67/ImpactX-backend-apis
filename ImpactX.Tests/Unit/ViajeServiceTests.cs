using ImpactX.Core.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Core.Pagination;
using ImpactX.Core.Security;
using ImpactX.Core.Wearables;
using ImpactX.Models.DTOs;
using ImpactX.Services;

namespace ImpactX.Tests.Unit;

public class ViajeServiceTests
{
    private readonly Mock<IViajeRepository> _viajeRepo;
    private readonly Mock<IWearableRepository> _wearableRepo;
    private readonly ViajeService _viajeService;

    public ViajeServiceTests()
    {
        _viajeRepo = new Mock<IViajeRepository>();
        _wearableRepo = new Mock<IWearableRepository>();
        var logger = Mock.Of<ILogger<ViajeService>>();
        _viajeService = new ViajeService(
            _viajeRepo.Object,
            logger,
            wearableRepository: _wearableRepo.Object);
    }

    [Fact]
    public async Task StartAsync_CreatesActiveTrip()
    {
        var usuarioId = Guid.NewGuid();
        _viajeRepo.Setup(r => r.GetActiveByUserAsync(usuarioId)).ReturnsAsync((Viaje?)null);

        var result = await _viajeService.StartAsync(usuarioId, new StartTripRequest
        {
            DispositivoId = "WEAR-001",
            Proposito = "Trabajo",
        });

        Assert.Equal("Activo", result.Estado);
        Assert.Equal("WEAR-001", result.DispositivoId);
        _viajeRepo.Verify(r => r.AddAsync(It.IsAny<Viaje>()), Times.Once);
    }

    [Fact]
    public async Task StartAsync_WithActiveTrip_Throws()
    {
        var usuarioId = Guid.NewGuid();
        _viajeRepo.Setup(r => r.GetActiveByUserAsync(usuarioId))
            .ReturnsAsync(new Viaje { Estado = "Activo" });

        await Assert.ThrowsAsync<ConflictException>(() =>
            _viajeService.StartAsync(usuarioId, new StartTripRequest { DispositivoId = "WEAR-001" }));
    }

    [Fact]
    public async Task StartAsync_MobileRelayWithLinkedGalaxyWatch8_CreatesWearableControlledTrip()
    {
        var usuarioId = Guid.NewGuid();
        const string deviceId = "GW8-UNIT-001";
        _viajeRepo.Setup(r => r.GetActiveByUserAsync(usuarioId)).ReturnsAsync((Viaje?)null);
        _wearableRepo.Setup(r => r.GetByUsuarioIdAsync(usuarioId)).ReturnsAsync(new Wearable
        {
            UsuarioId = usuarioId,
            DispositivoId = deviceId,
            Estado = "Vinculado",
            Modelo = WearableProductPolicy.TargetModel,
            Fabricante = WearableProductPolicy.TargetManufacturer,
            Plataforma = WearableProductPolicy.TargetPlatform,
        });

        var result = await _viajeService.StartAsync(usuarioId, new StartTripRequest
        {
            DispositivoId = deviceId,
            Client = ClientTypePolicy.Mobile,
        });

        Assert.Equal(ClientTypePolicy.Wearable, result.ControlClient);
        Assert.True(result.MobileFallbackUsed);
        Assert.False(string.IsNullOrWhiteSpace(result.FallbackReason));
        _viajeRepo.Verify(r => r.AddAsync(It.Is<Viaje>(trip =>
            trip.DispositivoId == deviceId
            && trip.ControlClient == ClientTypePolicy.Wearable
            && trip.MobileFallbackUsed)), Times.Once);
    }

    [Fact]
    public async Task StartAsync_MobileRelayWithMismatchedWearable_ThrowsForbidden()
    {
        var usuarioId = Guid.NewGuid();
        _wearableRepo.Setup(r => r.GetByUsuarioIdAsync(usuarioId)).ReturnsAsync(new Wearable
        {
            UsuarioId = usuarioId,
            DispositivoId = "GW8-OWNED",
            Estado = "Vinculado",
            Modelo = WearableProductPolicy.TargetModel,
            Fabricante = WearableProductPolicy.TargetManufacturer,
            Plataforma = WearableProductPolicy.TargetPlatform,
        });

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _viajeService.StartAsync(usuarioId, new StartTripRequest
            {
                DispositivoId = "GW8-OTHER",
                Client = ClientTypePolicy.Mobile,
            }));

        _viajeRepo.Verify(r => r.AddAsync(It.IsAny<Viaje>()), Times.Never);
    }

    [Fact]
    public async Task PauseAsync_PausesActiveTrip()
    {
        var usuarioId = Guid.NewGuid();
        var viaje = new Viaje { Id = Guid.NewGuid(), UsuarioId = usuarioId, Estado = "Activo" };
        _viajeRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), viaje.Id)).ReturnsAsync(viaje);

        var result = await _viajeService.PauseAsync(usuarioId, viaje.Id);

        Assert.Equal("Pausado", result.Estado);
        Assert.Equal("Pausado", viaje.Estado);
        _viajeRepo.Verify(r => r.UpdateAsync(viaje), Times.Once);
    }

    [Fact]
    public async Task PauseAsync_NotActive_Throws()
    {
        var usuarioId = Guid.NewGuid();
        var viaje = new Viaje { Id = Guid.NewGuid(), UsuarioId = usuarioId, Estado = "Finalizado" };
        _viajeRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), viaje.Id)).ReturnsAsync(viaje);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _viajeService.PauseAsync(usuarioId, viaje.Id));
    }

    [Fact]
    public async Task ResumeAsync_ResumesPausedTrip()
    {
        var usuarioId = Guid.NewGuid();
        var viaje = new Viaje { Id = Guid.NewGuid(), UsuarioId = usuarioId, Estado = "Pausado" };
        _viajeRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), viaje.Id)).ReturnsAsync(viaje);

        var result = await _viajeService.ResumeAsync(usuarioId, viaje.Id);

        Assert.Equal("Activo", result.Estado);
        Assert.Equal("Activo", viaje.Estado);
    }

    [Fact]
    public async Task FinishAsync_CalculatesDistanceAndDuration()
    {
        var usuarioId = Guid.NewGuid();
        var viaje = new Viaje
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            Estado = "Activo",
            Inicio = DateTime.UtcNow.AddMinutes(-30),
        };
        _viajeRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), viaje.Id)).ReturnsAsync(viaje);
        _viajeRepo.Setup(r => r.GetTelemetryByViajeAsync(viaje.Id))
            .ReturnsAsync([
                new ViajeTelemetry { Lat = 19.43, Lng = -99.13, Velocidad = 50 },
                new ViajeTelemetry { Lat = 19.44, Lng = -99.14, Velocidad = 60 },
            ]);

        var result = await _viajeService.FinishAsync(usuarioId, viaje.Id);

        Assert.Equal("Finalizado", result.Estado);
        Assert.NotNull(result.Fin);
        Assert.NotNull(result.DuracionMinutos);
        Assert.True(result.DistanciaRecorridaKm > 0);
        Assert.Equal(55, result.VelocidadPromedio);
        Assert.Equal(60, result.VelocidadMaxima);
        _viajeRepo.Verify(r => r.UpdateAsync(viaje), Times.Once);
    }

    [Fact]
    public async Task FinishAsync_AlreadyFinished_Throws()
    {
        var usuarioId = Guid.NewGuid();
        var viaje = new Viaje { Id = Guid.NewGuid(), UsuarioId = usuarioId, Estado = "Finalizado" };
        _viajeRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), viaje.Id)).ReturnsAsync(viaje);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _viajeService.FinishAsync(usuarioId, viaje.Id));
    }

    [Fact]
    public async Task UpdateTelemetryAsync_SavesPoints()
    {
        var usuarioId = Guid.NewGuid();
        var viaje = new Viaje { Id = Guid.NewGuid(), UsuarioId = usuarioId, Estado = "Activo" };
        _viajeRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), viaje.Id)).ReturnsAsync(viaje);

        var result = await _viajeService.UpdateTelemetryAsync(usuarioId, viaje.Id, new TelemetryUpdateRequest
        {
            Puntos = [
                new TelemetryPointDto { Lat = 19.43, Lng = -99.13, Velocidad = 50, Timestamp = DateTime.UtcNow },
                new TelemetryPointDto { Lat = 19.44, Lng = -99.14, Velocidad = 60, Timestamp = DateTime.UtcNow },
            ]
        });

        Assert.Equal(2, result.Count);
        _viajeRepo.Verify(r => r.AddTelemetryAsync(It.IsAny<ViajeTelemetry>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GetActiveAsync_ReturnsActiveTrip()
    {
        var usuarioId = Guid.NewGuid();
        var viaje = new Viaje { Id = Guid.NewGuid(), UsuarioId = usuarioId, Estado = "Activo" };
        _viajeRepo.Setup(r => r.GetActiveByUserAsync(usuarioId)).ReturnsAsync(viaje);

        var result = await _viajeService.GetActiveAsync(usuarioId);

        Assert.NotNull(result);
        Assert.Equal("Activo", result!.Estado);
    }

    [Fact]
    public async Task GetActiveAsync_NoActiveTrip_ReturnsNull()
    {
        _viajeRepo.Setup(r => r.GetActiveByUserAsync(It.IsAny<Guid>())).ReturnsAsync((Viaje?)null);

        var result = await _viajeService.GetActiveAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task AccessOtherUsersTrip_Throws()
    {
        var usuarioId = Guid.NewGuid();
        var otroUsuarioId = Guid.NewGuid();
        var viaje = new Viaje { Id = Guid.NewGuid(), UsuarioId = otroUsuarioId, Estado = "Activo" };
        _viajeRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), viaje.Id)).ReturnsAsync(viaje);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _viajeService.PauseAsync(usuarioId, viaje.Id));
    }

    [Fact]
    public async Task GetTripsPagedAsync_ReturnsMappedPage()
    {
        var usuarioId = Guid.NewGuid();
        var viaje = new Viaje { Id = Guid.NewGuid(), UsuarioId = usuarioId, Estado = "Activo" };
        var page = new PagedResult<Viaje>
        {
            Items = [viaje],
            ContinuationToken = "token",
            HasMoreResults = true,
            PageSize = 20,
        };
        _viajeRepo.Setup(r => r.GetByUserPagedAsync(usuarioId, 20, null)).ReturnsAsync(page);

        var result = await _viajeService.GetTripsPagedAsync(usuarioId, null, null);

        Assert.Single(result.Items);
        Assert.Equal(viaje.Id, result.Items[0].Id);
        Assert.Equal("token", result.ContinuationToken);
        Assert.True(result.HasMoreResults);
    }

    [Fact]
    public async Task GetTripsPagedAsync_OutOfRangePageSize_ThrowsBadRequest()
    {
        await Assert.ThrowsAsync<BadRequestException>(() =>
            _viajeService.GetTripsPagedAsync(Guid.NewGuid(), 101, null));
    }

    [Fact]
    public async Task GetTelemetryPagedAsync_OwnTrip_ReturnsPage()
    {
        var usuarioId = Guid.NewGuid();
        var viaje = new Viaje { Id = Guid.NewGuid(), UsuarioId = usuarioId, Estado = "Finalizado" };
        var punto = new ViajeTelemetry { Id = Guid.NewGuid(), ViajeId = viaje.Id, Lat = 19.43, Lng = -99.13, Velocidad = 50 };
        var page = new PagedResult<ViajeTelemetry>
        {
            Items = [punto],
            ContinuationToken = null,
            HasMoreResults = false,
            PageSize = 20,
        };
        _viajeRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), viaje.Id)).ReturnsAsync(viaje);
        _viajeRepo.Setup(r => r.GetTelemetryByViajePagedAsync(viaje.Id, 20, null)).ReturnsAsync(page);

        var result = await _viajeService.GetTelemetryPagedAsync(usuarioId, viaje.Id, null, null);

        Assert.Single(result.Items);
        Assert.Equal(19.43, result.Items[0].Lat);
        Assert.False(result.HasMoreResults);
    }

    [Fact]
    public async Task GetTelemetryPagedAsync_OtherUsersTrip_Throws()
    {
        var usuarioId = Guid.NewGuid();
        var otroUsuarioId = Guid.NewGuid();
        var viaje = new Viaje { Id = Guid.NewGuid(), UsuarioId = otroUsuarioId, Estado = "Finalizado" };
        _viajeRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), viaje.Id)).ReturnsAsync(viaje);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _viajeService.GetTelemetryPagedAsync(usuarioId, viaje.Id, null, null));
    }
}
