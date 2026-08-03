using ImpactX.Core.Domain;
using ImpactX.Core.Exceptions;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Models.DTOs;
using ImpactX.Services;
using Moq;

namespace ImpactX.Tests.Unit;

public class WearableGalaxyWatch8ServiceTests
{
    private readonly Mock<IWearableRepository> _wearableRepository = new();
    private readonly Mock<IUsuarioRepository> _userRepository = new();
    private readonly Mock<IPlanService> _planService = new();

    private WearableService CreateService()
        => new(_wearableRepository.Object, _userRepository.Object, _planService.Object);

    private static PairWearableRequest ValidPairRequest(string deviceId = "GW8-001") => new()
    {
        DispositivoId = deviceId,
        Nombre = "Galaxy Watch 8 de prueba",
        Modelo = "Galaxy Watch 8",
        Fabricante = "Samsung",
        Plataforma = "WearOS",
        AppVersion = "1.0.0",
        VersionSistemaOperativo = "WearOS-test",
        VersionFirmware = "FW-test",
        CapacidadesSensores = ["gps", "accelerometer", "gyroscope", "heart_rate", "hrv", "spo2"],
    };

    private Wearable Linked(Guid userId, string deviceId = "GW8-001") => new()
    {
        Id = Guid.NewGuid(),
        UsuarioId = userId,
        DispositivoId = deviceId,
        Nombre = "Galaxy Watch 8",
        Modelo = "Galaxy Watch 8",
        Fabricante = "Samsung",
        Plataforma = "WearOS",
        Estado = "Vinculado",
    };

    [Fact]
    public async Task PairAsync_NonTargetDevice_ThrowsBadRequest()
    {
        var userId = Guid.NewGuid();
        _userRepository.Setup(value => value.GetByIdAsync(userId)).ReturnsAsync(new Usuario { Id = userId });

        var request = ValidPairRequest();
        request.Modelo = "Other Watch";

        await Assert.ThrowsAsync<BadRequestException>(() => CreateService().PairAsync(userId, request));
        _wearableRepository.Verify(value => value.AddAsync(It.IsAny<Wearable>()), Times.Never);
    }

    [Fact]
    public async Task PairAsync_TargetDevice_SetsCanonicalMetadataAndExpiration()
    {
        var userId = Guid.NewGuid();
        _userRepository.Setup(value => value.GetByIdAsync(userId)).ReturnsAsync(new Usuario { Id = userId });
        _planService.Setup(value => value.GetCurrentSubscriptionAsync(userId))
            .ReturnsAsync(new SuscripcionDto { PlanNombre = "Free" });
        _wearableRepository.Setup(value => value.GetAllByUsuarioIdAsync(userId)).ReturnsAsync([]);
        _wearableRepository.Setup(value => value.GetByDispositivoIdAsync("GW8-001"))
            .ReturnsAsync((Wearable?)null);

        var before = DateTime.UtcNow;
        var result = await CreateService().PairAsync(userId, ValidPairRequest());

        Assert.Equal(8, result.Token.Length);
        Assert.True(result.ExpiresAtUtc > before);
        _wearableRepository.Verify(value => value.AddAsync(It.Is<Wearable>(wearable =>
            wearable.PairingToken != result.Token
            && wearable.PairingToken != null
            && wearable.PairingToken.Length == 64
            && wearable.Modelo == "Galaxy Watch 8"
            && wearable.Fabricante == "Samsung"
            && wearable.Plataforma == "WearOS"
            && wearable.PairingExpiresAtUtc == result.ExpiresAtUtc
            && wearable.CapacidadesSensores.Contains("accelerometer"))), Times.Once);
    }

    [Fact]
    public async Task PairConfirmAsync_ExpiredToken_InvalidatesPendingPairing()
    {
        var userId = Guid.NewGuid();
        var wearable = Linked(userId);
        wearable.Estado = "Pendiente";
        wearable.PairingToken = "ABC12345";
        wearable.PairingExpiresAtUtc = DateTime.UtcNow.AddSeconds(-1);
        _wearableRepository.Setup(value => value.GetByPairingTokenAsync("ABC12345")).ReturnsAsync(wearable);

        await Assert.ThrowsAsync<ConflictException>(() => CreateService().PairConfirmAsync(
            userId,
            new PairConfirmRequest { Token = "ABC12345" }));

        Assert.Equal("Expirado", wearable.Estado);
        Assert.Null(wearable.PairingToken);
        _wearableRepository.Verify(value => value.UpdateAsync(wearable), Times.Once);
    }

    [Fact]
    public async Task RegisterHeartbeatAsync_UpdatesConnectionAndVersions()
    {
        var userId = Guid.NewGuid();
        var wearable = Linked(userId);
        _wearableRepository.Setup(value => value.GetByUsuarioIdAsync(userId)).ReturnsAsync(wearable);

        var result = await CreateService().RegisterHeartbeatAsync(userId, new WearableHeartbeatRequest
        {
            DispositivoId = wearable.DispositivoId,
            Modelo = "Galaxy Watch 8",
            Fabricante = "Samsung",
            Plataforma = "WearOS",
            AppVersion = "2.0.0",
            VersionSistemaOperativo = "WearOS-test-2",
            VersionFirmware = "FW-2",
            NivelBateria = 68,
            Cargando = true,
            DesfaseRelojMilisegundos = 42,
            CapacidadesSensores = ["GPS", "Accelerometer", "Gyroscope"],
            TimestampUtc = DateTime.UtcNow,
        });

        Assert.True(result.Connected);
        Assert.True(result.Cargando);
        Assert.Equal(68, result.NivelBateria);
        Assert.Equal("2.0.0", result.AppVersion);
        Assert.Equal(new[] { "accelerometer", "gps", "gyroscope" }, result.CapacidadesSensores);
        Assert.NotNull(result.UltimoHeartbeatUtc);
        _wearableRepository.Verify(value => value.UpdateAsync(wearable), Times.Once);
    }

    [Fact]
    public async Task RegisterHeartbeatAsync_WrongDevice_ReturnsNotFound()
    {
        var userId = Guid.NewGuid();
        _wearableRepository.Setup(value => value.GetByUsuarioIdAsync(userId)).ReturnsAsync(Linked(userId));

        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().RegisterHeartbeatAsync(
            userId,
            new WearableHeartbeatRequest
            {
                DispositivoId = "OTHER",
                Modelo = "Galaxy Watch 8",
                Fabricante = "Samsung",
                Plataforma = "WearOS",
                NivelBateria = 80,
                TimestampUtc = DateTime.UtcNow,
            }));
    }

    [Fact]
    public async Task ReportDiagnosticsAsync_OverlappingSensorStatus_ThrowsBadRequest()
    {
        var userId = Guid.NewGuid();
        var wearable = Linked(userId);
        _wearableRepository.Setup(value => value.GetByUsuarioIdAsync(userId)).ReturnsAsync(wearable);

        await Assert.ThrowsAsync<BadRequestException>(() => CreateService().ReportDiagnosticsAsync(
            userId,
            new WearableDiagnosticsReportRequest
            {
                DispositivoId = wearable.DispositivoId,
                CalidadGeneral = "high",
                SensoresDisponibles = ["gps"],
                SensoresNoDisponibles = ["GPS"],
                TimestampUtc = DateTime.UtcNow,
            }));

        _wearableRepository.Verify(value => value.UpdateAsync(It.IsAny<Wearable>()), Times.Never);
    }

    [Fact]
    public async Task ReportDiagnosticsAsync_PersistsRealSensorState()
    {
        var userId = Guid.NewGuid();
        var wearable = Linked(userId);
        _wearableRepository.Setup(value => value.GetByUsuarioIdAsync(userId)).ReturnsAsync(wearable);

        var result = await CreateService().ReportDiagnosticsAsync(
            userId,
            new WearableDiagnosticsReportRequest
            {
                DispositivoId = wearable.DispositivoId,
                CalidadGeneral = "MEDIUM",
                SensoresDisponibles = ["accelerometer", "gyroscope", "gps", "heart_rate"],
                SensoresNoDisponibles = ["spo2"],
                TimestampUtc = DateTime.UtcNow,
            });

        Assert.Equal("medium", result.CalidadSensores);
        Assert.Contains("gps", result.SensoresDisponibles);
        Assert.Contains("spo2", result.SensoresNoDisponibles);
        Assert.NotNull(result.UltimoDiagnosticoUtc);
    }
}
