using Moq;
using Prueba1.Core.Domain;
using Prueba1.Core.Interfaces.Repositories;
using Prueba1.Models.DTOs;
using Prueba1.Services;

namespace Prueba1.Tests.Unit;

public class WearableServiceTests
{
    private readonly Mock<IWearableRepository> _wearableRepo;
    private readonly Mock<IUsuarioRepository> _usuarioRepo;
    private readonly Mock<IPlanService> _planService;
    private readonly WearableService _wearableService;

    public WearableServiceTests()
    {
        _wearableRepo = new Mock<IWearableRepository>();
        _usuarioRepo = new Mock<IUsuarioRepository>();
        _planService = new Mock<IPlanService>();
        _wearableService = new WearableService(
            _wearableRepo.Object,
            _usuarioRepo.Object,
            _planService.Object);
    }

    [Fact]
    public async Task GetWearableAsync_WithExistingWearable_ReturnsDto()
    {
        var usuarioId = Guid.NewGuid();
        var wearable = new Wearable
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            DispositivoId = "DEV-001",
            Nombre = "Apple Watch",
            Modelo = "Series 9",
            Estado = "Vinculado",
        };

        _wearableRepo.Setup(r => r.GetByUsuarioIdAsync(usuarioId)).ReturnsAsync(wearable);

        var result = await _wearableService.GetWearableAsync(usuarioId);

        Assert.NotNull(result);
        Assert.Equal("DEV-001", result!.DispositivoId);
        Assert.Equal("Apple Watch", result.Nombre);
    }

    [Fact]
    public async Task GetWearableAsync_WithoutWearable_ReturnsNull()
    {
        var usuarioId = Guid.NewGuid();
        _wearableRepo.Setup(r => r.GetByUsuarioIdAsync(usuarioId)).ReturnsAsync((Wearable?)null);

        var result = await _wearableService.GetWearableAsync(usuarioId);

        Assert.Null(result);
    }

    [Fact]
    public async Task PairAsync_WithFreePlanAndNoExistingWearable_ReturnsToken()
    {
        var usuarioId = Guid.NewGuid();
        var usuario = new Usuario { Id = usuarioId, PlanActivo = "Free" };

        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);
        _planService.Setup(s => s.GetCurrentSubscriptionAsync(usuarioId))
            .ReturnsAsync(new SuscripcionDto { PlanNombre = "Free" });
        _wearableRepo.Setup(r => r.GetAllByUsuarioIdAsync(usuarioId)).ReturnsAsync([]);
        _wearableRepo.Setup(r => r.GetByDispositivoIdAsync(It.IsAny<string>())).ReturnsAsync((Wearable?)null);

        var result = await _wearableService.PairAsync(usuarioId, new PairWearableRequest
        {
            DispositivoId = "DEV-001",
            Nombre = "Apple Watch",
            Modelo = "Series 9",
        });

        Assert.NotNull(result.Token);
        Assert.Equal(8, result.Token.Length);
        _wearableRepo.Verify(r => r.AddAsync(It.IsAny<Wearable>()), Times.Once);
    }

    [Fact]
    public async Task PairAsync_WithFreePlanAndExistingWearable_Throws()
    {
        var usuarioId = Guid.NewGuid();
        var usuario = new Usuario { Id = usuarioId, PlanActivo = "Free" };

        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);
        _planService.Setup(s => s.GetCurrentSubscriptionAsync(usuarioId))
            .ReturnsAsync(new SuscripcionDto { PlanNombre = "Free" });
        _wearableRepo.Setup(r => r.GetAllByUsuarioIdAsync(usuarioId))
            .ReturnsAsync([new Wearable { Estado = "Vinculado" }]);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _wearableService.PairAsync(usuarioId, new PairWearableRequest
            {
                DispositivoId = "DEV-002",
                Nombre = "Test",
                Modelo = "Test",
            }));
    }

    [Fact]
    public async Task PairAsync_WithExistingDispositivoId_Throws()
    {
        var usuarioId = Guid.NewGuid();
        var usuario = new Usuario { Id = usuarioId, PlanActivo = "Premium" };

        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);
        _planService.Setup(s => s.GetCurrentSubscriptionAsync(usuarioId))
            .ReturnsAsync(new SuscripcionDto { PlanNombre = "Premium" });
        _wearableRepo.Setup(r => r.GetAllByUsuarioIdAsync(usuarioId)).ReturnsAsync([]);
        _wearableRepo.Setup(r => r.GetByDispositivoIdAsync("DEV-001"))
            .ReturnsAsync(new Wearable());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _wearableService.PairAsync(usuarioId, new PairWearableRequest
            {
                DispositivoId = "DEV-001",
                Nombre = "Test",
                Modelo = "Test",
            }));
    }

    [Fact]
    public async Task PairConfirmAsync_WithValidToken_ReturnsDto()
    {
        var usuarioId = Guid.NewGuid();
        var wearable = new Wearable
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            DispositivoId = "DEV-001",
            PairingToken = "ABC12345",
            Estado = "Pendiente",
        };

        _wearableRepo.Setup(r => r.GetByPairingTokenAsync("ABC12345")).ReturnsAsync(wearable);

        var result = await _wearableService.PairConfirmAsync(usuarioId, new PairConfirmRequest
        {
            Token = "ABC12345"
        });

        Assert.Equal("Vinculado", result.Estado);
        Assert.True(result.Connected);
        _wearableRepo.Verify(r => r.UpdateAsync(wearable), Times.Once);
    }

    [Fact]
    public async Task PairConfirmAsync_WithInvalidToken_Throws()
    {
        _wearableRepo.Setup(r => r.GetByPairingTokenAsync("INVALID")).ReturnsAsync((Wearable?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _wearableService.PairConfirmAsync(Guid.NewGuid(), new PairConfirmRequest
            {
                Token = "INVALID"
            }));
    }

    [Fact]
    public async Task PairConfirmAsync_WithWrongUser_Throws()
    {
        var usuarioId = Guid.NewGuid();
        var otroUsuarioId = Guid.NewGuid();
        var wearable = new Wearable
        {
            UsuarioId = otroUsuarioId,
            PairingToken = "TOKEN123",
            Estado = "Pendiente",
        };

        _wearableRepo.Setup(r => r.GetByPairingTokenAsync("TOKEN123")).ReturnsAsync(wearable);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _wearableService.PairConfirmAsync(usuarioId, new PairConfirmRequest { Token = "TOKEN123" }));
    }

    [Fact]
    public async Task SyncAsync_UpdatesLastSync()
    {
        var usuarioId = Guid.NewGuid();
        var wearable = new Wearable
        {
            UsuarioId = usuarioId,
            Estado = "Vinculado",
        };

        _wearableRepo.Setup(r => r.GetByUsuarioIdAsync(usuarioId)).ReturnsAsync(wearable);

        var result = await _wearableService.SyncAsync(usuarioId, new SyncTelemetryRequest
        {
            Puntos = [new TelemetryPointDto { Lat = 19.43, Lng = -99.13, Velocidad = 60 }]
        });

        Assert.Single(result);
        Assert.NotNull(wearable.UltimaSincronizacion);
        Assert.True(wearable.Connected);
        _wearableRepo.Verify(r => r.UpdateAsync(wearable), Times.Once);
    }

    [Fact]
    public async Task UnlinkAsync_MarksAsDesvinculado()
    {
        var usuarioId = Guid.NewGuid();
        var wearable = new Wearable
        {
            UsuarioId = usuarioId,
            Estado = "Vinculado",
            Connected = true,
        };

        _wearableRepo.Setup(r => r.GetAllByUsuarioIdAsync(usuarioId)).ReturnsAsync([wearable]);

        await _wearableService.UnlinkAsync(usuarioId);

        Assert.Equal("Desvinculado", wearable.Estado);
        Assert.False(wearable.Connected);
        _wearableRepo.Verify(r => r.UpdateAsync(wearable), Times.Once);
    }

    [Fact]
    public async Task UnlinkAsync_WithoutWearable_Throws()
    {
        _wearableRepo.Setup(r => r.GetAllByUsuarioIdAsync(It.IsAny<Guid>())).ReturnsAsync([]);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _wearableService.UnlinkAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task UpdatePermissionsAsync_UpdatesPermissions()
    {
        var usuarioId = Guid.NewGuid();
        var wearable = new Wearable
        {
            UsuarioId = usuarioId,
            Estado = "Vinculado",
        };

        _wearableRepo.Setup(r => r.GetByUsuarioIdAsync(usuarioId)).ReturnsAsync(wearable);

        var result = await _wearableService.UpdatePermissionsAsync(usuarioId, new UpdateWearablePermissionsRequest
        {
            Permisos = ["ubicacion", "notificaciones"]
        });

        Assert.Equal(2, result.PermisosOtorgados.Count);
        _wearableRepo.Verify(r => r.UpdateAsync(wearable), Times.Once);
    }

    [Fact]
    public async Task UpdateBatteryAsync_ClampsValue()
    {
        var usuarioId = Guid.NewGuid();
        var wearable = new Wearable { UsuarioId = usuarioId, Estado = "Vinculado" };

        _wearableRepo.Setup(r => r.GetByUsuarioIdAsync(usuarioId)).ReturnsAsync(wearable);

        var result = await _wearableService.UpdateBatteryAsync(usuarioId, new BatteryUpdateRequest
        {
            Nivel = 150
        });

        Assert.Equal(100, result.NivelBateria);
    }

    [Fact]
    public async Task GetSensorDiagnosticsAsync_ReturnsDiagnostics()
    {
        var usuarioId = Guid.NewGuid();
        var wearable = new Wearable
        {
            UsuarioId = usuarioId,
            Estado = "Vinculado",
            NivelBateria = 85,
        };

        _wearableRepo.Setup(r => r.GetByUsuarioIdAsync(usuarioId)).ReturnsAsync(wearable);

        var result = await _wearableService.GetSensorDiagnosticsAsync(usuarioId);

        Assert.True(result.Acelerometro);
        Assert.True(result.Gps);
        Assert.Equal(85, result.NivelBateria);
    }

    [Fact]
    public async Task CalibrateAsync_MarksAsCalibrado()
    {
        var usuarioId = Guid.NewGuid();
        var wearable = new Wearable { UsuarioId = usuarioId, Estado = "Vinculado" };

        _wearableRepo.Setup(r => r.GetByUsuarioIdAsync(usuarioId)).ReturnsAsync(wearable);

        var result = await _wearableService.CalibrateAsync(usuarioId, new CalibrationRequest
        {
            Acelerometro = true,
            Gps = true,
        });

        Assert.True(result.Calibrado);
        Assert.NotNull(result.UltimaCalibracion);
        _wearableRepo.Verify(r => r.UpdateAsync(wearable), Times.Once);
    }
}
