using ImpactX.Core.Domain;
using ImpactX.Core.Exceptions;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Models.DTOs;
using ImpactX.Services;
using Moq;

namespace ImpactX.Tests.Unit;

public class DeviceServiceTests
{
    private readonly Mock<IDispositivoRepository> _dispositivoRepo;
    private readonly Mock<IUsuarioRepository> _usuarioRepo;
    private readonly ListLogger<DeviceService> _logger;
    private readonly DeviceService _deviceService;

    public DeviceServiceTests()
    {
        _dispositivoRepo = new Mock<IDispositivoRepository>();
        _usuarioRepo = new Mock<IUsuarioRepository>();
        _logger = new ListLogger<DeviceService>();
        _deviceService = new DeviceService(_dispositivoRepo.Object, _usuarioRepo.Object, _logger);
    }

    private static UpsertDeviceRequest ValidRequest(string deviceId = "phone-1", string token = "token-1", string platform = "Android") => new()
    {
        DeviceId = deviceId,
        Platform = platform,
        Token = token,
        Name = "Mi teléfono",
    };

    private static Usuario LegacyUser(Guid usuarioId, string? fcmToken) => new()
    {
        Id = usuarioId,
        Nombre = "Test",
        Correo = "test@example.com",
        FcmToken = fcmToken,
    };

    [Fact]
    public async Task GetDevicesAsync_ReturnsOnlyOwnDevices()
    {
        var usuarioId = Guid.NewGuid();
        var otherUserDevice = new Dispositivo
        {
            Id = Guid.NewGuid(),
            UsuarioId = Guid.NewGuid(),
            DeviceId = "phone-b",
            TokenFcm = "token-b",
            Platform = "Web",
        };
        var ownDevice = new Dispositivo
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            DeviceId = "phone-a",
            TokenFcm = "token-a",
            Platform = "Android",
            Nombre = "Moto",
        };

        _dispositivoRepo.Setup(r => r.GetByUsuarioIdAsync(usuarioId)).ReturnsAsync([ownDevice]);

        var result = await _deviceService.GetDevicesAsync(usuarioId);

        Assert.Single(result);
        Assert.Equal(ownDevice.Id, result[0].Id);
        Assert.Equal("phone-a", result[0].DeviceId);
        Assert.Equal("Android", result[0].Platform);
        Assert.Equal("Moto", result[0].Nombre);
    }

    [Fact]
    public void DeviceDto_DoesNotExposeTokenFcm()
    {
        var dtoProperties = typeof(DeviceDto).GetProperties().Select(p => p.Name);

        Assert.DoesNotContain("TokenFcm", dtoProperties);
        Assert.DoesNotContain("tokenFcm", dtoProperties, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpsertFcmTokenAsync_NewDevice_Creates()
    {
        var usuarioId = Guid.NewGuid();
        _dispositivoRepo.Setup(r => r.GetByDeviceIdAsync(usuarioId, "phone-1")).ReturnsAsync((Dispositivo?)null);

        await _deviceService.UpsertFcmTokenAsync(usuarioId, ValidRequest());

        _dispositivoRepo.Verify(r => r.AddAsync(It.Is<Dispositivo>(d =>
            d.UsuarioId == usuarioId &&
            d.DeviceId == "phone-1" &&
            d.TokenFcm == "token-1" &&
            d.Platform == "Android" &&
            d.Nombre == "Mi teléfono" &&
            d.Activo)), Times.Once);
    }

    [Fact]
    public async Task UpsertFcmTokenAsync_ExistingDevice_ReplacesToken()
    {
        var usuarioId = Guid.NewGuid();
        var dispositivo = new Dispositivo
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            DeviceId = "phone-1",
            TokenFcm = "old-token",
            Platform = "Android",
            Activo = true,
        };

        _dispositivoRepo.Setup(r => r.GetByDeviceIdAsync(usuarioId, "phone-1")).ReturnsAsync(dispositivo);
        _dispositivoRepo.Setup(r => r.GetByTokenFcmAsync("new-token")).ReturnsAsync((Dispositivo?)null);

        await _deviceService.UpsertFcmTokenAsync(usuarioId, ValidRequest(token: "new-token"));

        Assert.Equal("new-token", dispositivo.TokenFcm);
        Assert.True(dispositivo.Activo);
        Assert.NotNull(dispositivo.UltimoUsoEn);
        _dispositivoRepo.Verify(r => r.UpdateAsync(dispositivo), Times.Once);
        _dispositivoRepo.Verify(r => r.AddAsync(It.IsAny<Dispositivo>()), Times.Never);
    }

    [Fact]
    public async Task UpsertFcmTokenAsync_SameTokenOnAnotherDevice_RemovesDuplicate()
    {
        var usuarioId = Guid.NewGuid();
        var dispositivo = new Dispositivo
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            DeviceId = "phone-1",
            TokenFcm = "old-token",
            Platform = "Android",
        };
        var duplicado = new Dispositivo
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            DeviceId = "watch-1",
            TokenFcm = "new-token",
            Platform = "WearOS",
        };

        _dispositivoRepo.Setup(r => r.GetByDeviceIdAsync(usuarioId, "phone-1")).ReturnsAsync(dispositivo);
        _dispositivoRepo.Setup(r => r.GetByTokenFcmAsync("new-token")).ReturnsAsync(duplicado);

        await _deviceService.UpsertFcmTokenAsync(usuarioId, ValidRequest(token: "new-token"));

        Assert.Equal("new-token", dispositivo.TokenFcm);
        Assert.Equal(string.Empty, duplicado.TokenFcm);
        Assert.False(duplicado.Activo);
        _dispositivoRepo.Verify(r => r.UpdateAsync(duplicado), Times.Once);
        _dispositivoRepo.Verify(r => r.UpdateAsync(dispositivo), Times.Once);
    }

    [Fact]
    public async Task UpsertFcmTokenAsync_NewDevice_TokenUsedBySameUserDevice_DeactivatesPrevious()
    {
        var usuarioId = Guid.NewGuid();
        var previous = new Dispositivo
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            DeviceId = "watch-1",
            TokenFcm = "shared-token",
            Platform = "WearOS",
        };

        _dispositivoRepo.Setup(r => r.GetByDeviceIdAsync(usuarioId, "phone-1")).ReturnsAsync((Dispositivo?)null);
        _dispositivoRepo.Setup(r => r.GetByTokenFcmAsync("shared-token")).ReturnsAsync(previous);

        await _deviceService.UpsertFcmTokenAsync(usuarioId, ValidRequest(token: "shared-token"));

        Assert.Equal(string.Empty, previous.TokenFcm);
        Assert.False(previous.Activo);
        _dispositivoRepo.Verify(r => r.UpdateAsync(previous), Times.Once);
        _dispositivoRepo.Verify(r => r.AddAsync(It.Is<Dispositivo>(d =>
            d.DeviceId == "phone-1" && d.TokenFcm == "shared-token" && d.Activo)), Times.Once);
    }

    [Fact]
    public async Task UpsertFcmTokenAsync_SameDeviceReusesItsOwnToken_NoOp()
    {
        var usuarioId = Guid.NewGuid();
        var dispositivo = new Dispositivo
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            DeviceId = "phone-1",
            TokenFcm = "same-token",
            Platform = "Android",
            Activo = true,
        };

        _dispositivoRepo.Setup(r => r.GetByDeviceIdAsync(usuarioId, "phone-1")).ReturnsAsync(dispositivo);
        _dispositivoRepo.Setup(r => r.GetByTokenFcmAsync("same-token")).ReturnsAsync(dispositivo);

        await _deviceService.UpsertFcmTokenAsync(usuarioId, ValidRequest(token: "same-token"));

        _dispositivoRepo.Verify(r => r.UpdateAsync(dispositivo), Times.Once);
        _dispositivoRepo.Verify(r => r.UpdateAsync(It.Is<Dispositivo>(d => d.Id != dispositivo.Id)), Times.Never);
    }

    [Fact]
    public async Task UpsertFcmTokenAsync_TokenOwnedByAnotherUser_ThrowsConflict()
    {
        var usuarioId = Guid.NewGuid();
        var otherUserDevice = new Dispositivo
        {
            Id = Guid.NewGuid(),
            UsuarioId = Guid.NewGuid(),
            DeviceId = "phone-other",
            TokenFcm = "foreign-token",
            Platform = "Android",
            Activo = true,
        };

        _dispositivoRepo.Setup(r => r.GetByDeviceIdAsync(usuarioId, "phone-1")).ReturnsAsync((Dispositivo?)null);
        _dispositivoRepo.Setup(r => r.GetByTokenFcmAsync("foreign-token")).ReturnsAsync(otherUserDevice);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _deviceService.UpsertFcmTokenAsync(usuarioId, ValidRequest(token: "foreign-token")));

        _dispositivoRepo.Verify(r => r.AddAsync(It.IsAny<Dispositivo>()), Times.Never);
        _dispositivoRepo.Verify(r => r.UpdateAsync(It.IsAny<Dispositivo>()), Times.Never);
        Assert.Equal("foreign-token", otherUserDevice.TokenFcm);
        Assert.True(otherUserDevice.Activo);
    }

    [Fact]
    public async Task UpsertFcmTokenAsync_Conflict_DoesNotLogToken()
    {
        var usuarioId = Guid.NewGuid();
        var secretToken = "conflict-secret-" + Guid.NewGuid().ToString("N");
        var otherUserDevice = new Dispositivo
        {
            Id = Guid.NewGuid(),
            UsuarioId = Guid.NewGuid(),
            DeviceId = "phone-other",
            TokenFcm = secretToken,
            Platform = "Android",
        };

        _dispositivoRepo.Setup(r => r.GetByDeviceIdAsync(usuarioId, "phone-1")).ReturnsAsync((Dispositivo?)null);
        _dispositivoRepo.Setup(r => r.GetByTokenFcmAsync(secretToken)).ReturnsAsync(otherUserDevice);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _deviceService.UpsertFcmTokenAsync(usuarioId, ValidRequest(token: secretToken)));

        foreach (var entry in _logger.LogEntries)
        {
            Assert.DoesNotContain(secretToken, entry, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("iOS")]
    [InlineData("Linux")]
    public async Task UpsertFcmTokenAsync_InvalidPlatform_Throws(string? platform)
    {
        var request = ValidRequest();
        request.Platform = platform!;

        await Assert.ThrowsAsync<BadRequestException>(() =>
            _deviceService.UpsertFcmTokenAsync(Guid.NewGuid(), request));
        _dispositivoRepo.Verify(r => r.AddAsync(It.IsAny<Dispositivo>()), Times.Never);
        _dispositivoRepo.Verify(r => r.UpdateAsync(It.IsAny<Dispositivo>()), Times.Never);
    }

    [Theory]
    [InlineData("android", "Android")]
    [InlineData("ANDROID", "Android")]
    [InlineData(" android ", "Android")]
    [InlineData("wearos", "WearOS")]
    [InlineData("WEAROS", "WearOS")]
    [InlineData(" WEAROS ", "WearOS")]
    [InlineData("web", "Web")]
    [InlineData("WEB", "Web")]
    public async Task UpsertFcmTokenAsync_NormalizesPlatformToCanonical(string input, string expected)
    {
        var usuarioId = Guid.NewGuid();
        _dispositivoRepo.Setup(r => r.GetByDeviceIdAsync(usuarioId, "phone-1")).ReturnsAsync((Dispositivo?)null);
        _dispositivoRepo.Setup(r => r.GetByTokenFcmAsync(It.IsAny<string>())).ReturnsAsync((Dispositivo?)null);

        await _deviceService.UpsertFcmTokenAsync(usuarioId, ValidRequest(platform: input));

        _dispositivoRepo.Verify(r => r.AddAsync(It.Is<Dispositivo>(d =>
            d.Platform == expected)), Times.Once);
    }

    [Fact]
    public async Task UpsertFcmTokenAsync_EmptyToken_Throws()
    {
        var request = ValidRequest(token: "");

        await Assert.ThrowsAsync<BadRequestException>(() =>
            _deviceService.UpsertFcmTokenAsync(Guid.NewGuid(), request));
    }

    [Fact]
    public async Task UpsertFcmTokenAsync_DoesNotLogToken()
    {
        var usuarioId = Guid.NewGuid();
        var secretToken = "fcm-secret-" + Guid.NewGuid().ToString("N");
        _dispositivoRepo.Setup(r => r.GetByDeviceIdAsync(usuarioId, "phone-1")).ReturnsAsync((Dispositivo?)null);

        await _deviceService.UpsertFcmTokenAsync(usuarioId, ValidRequest(token: secretToken));

        foreach (var entry in _logger.LogEntries)
        {
            Assert.DoesNotContain(secretToken, entry, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task UpsertFcmTokenAsync_ClearsLegacyFcmToken()
    {
        var usuarioId = Guid.NewGuid();
        var usuario = LegacyUser(usuarioId, "legacy-token");
        _dispositivoRepo.Setup(r => r.GetByDeviceIdAsync(usuarioId, "phone-1")).ReturnsAsync((Dispositivo?)null);
        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);

        await _deviceService.UpsertFcmTokenAsync(usuarioId, ValidRequest());

        Assert.Null(usuario.FcmToken);
        _usuarioRepo.Verify(r => r.UpdateAsync(usuario), Times.Once);
    }

    [Fact]
    public async Task DeleteDeviceAsync_OwnDevice_Deletes()
    {
        var usuarioId = Guid.NewGuid();
        var dispositivo = new Dispositivo { Id = Guid.NewGuid(), UsuarioId = usuarioId, DeviceId = "phone-1" };
        _dispositivoRepo.Setup(r => r.GetByIdAsync(usuarioId, dispositivo.Id)).ReturnsAsync(dispositivo);
        _dispositivoRepo.Setup(r => r.GetActiveByUsuarioIdAsync(usuarioId)).ReturnsAsync([dispositivo]);

        await _deviceService.DeleteDeviceAsync(usuarioId, dispositivo.Id);

        _dispositivoRepo.Verify(r => r.DeleteAsync(dispositivo), Times.Once);
    }

    [Fact]
    public async Task DeleteDeviceAsync_OtherUserDevice_ThrowsNotFound()
    {
        var usuarioId = Guid.NewGuid();
        var dispositivo = new Dispositivo { Id = Guid.NewGuid(), UsuarioId = Guid.NewGuid(), DeviceId = "phone-b" };

        _dispositivoRepo.Setup(r => r.GetByIdAsync(usuarioId, dispositivo.Id)).ReturnsAsync((Dispositivo?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _deviceService.DeleteDeviceAsync(usuarioId, dispositivo.Id));
        _dispositivoRepo.Verify(r => r.DeleteAsync(It.IsAny<Dispositivo>()), Times.Never);
    }

    [Fact]
    public async Task DeleteDeviceAsync_LastDevice_ClearsLegacyFcmToken()
    {
        var usuarioId = Guid.NewGuid();
        var dispositivo = new Dispositivo { Id = Guid.NewGuid(), UsuarioId = usuarioId, DeviceId = "phone-1" };
        var usuario = LegacyUser(usuarioId, "legacy-token");
        _dispositivoRepo.Setup(r => r.GetByIdAsync(usuarioId, dispositivo.Id)).ReturnsAsync(dispositivo);
        _dispositivoRepo.Setup(r => r.GetActiveByUsuarioIdAsync(usuarioId)).ReturnsAsync([]);
        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);

        await _deviceService.DeleteDeviceAsync(usuarioId, dispositivo.Id);

        Assert.Null(usuario.FcmToken);
        _usuarioRepo.Verify(r => r.UpdateAsync(usuario), Times.Once);
    }

    [Fact]
    public async Task DeleteDeviceAsync_StillOtherDevices_KeepsLegacyFcmToken()
    {
        var usuarioId = Guid.NewGuid();
        var dispositivo = new Dispositivo { Id = Guid.NewGuid(), UsuarioId = usuarioId, DeviceId = "phone-1" };
        var remaining = new Dispositivo { Id = Guid.NewGuid(), UsuarioId = usuarioId, DeviceId = "watch-1", Activo = true };
        _dispositivoRepo.Setup(r => r.GetByIdAsync(usuarioId, dispositivo.Id)).ReturnsAsync(dispositivo);
        _dispositivoRepo.Setup(r => r.GetActiveByUsuarioIdAsync(usuarioId)).ReturnsAsync([remaining]);

        await _deviceService.DeleteDeviceAsync(usuarioId, dispositivo.Id);

        _usuarioRepo.Verify(r => r.UpdateAsync(It.IsAny<Usuario>()), Times.Never);
    }

    [Fact]
    public async Task DeleteDeviceAsync_NotFound_Throws()
    {
        _dispositivoRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>())).ReturnsAsync((Dispositivo?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _deviceService.DeleteDeviceAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteAllDevicesAsync_RevokesAll()
    {
        var usuarioId = Guid.NewGuid();
        _dispositivoRepo.Setup(r => r.DeleteAllByUsuarioIdAsync(usuarioId, It.IsAny<CancellationToken>())).ReturnsAsync(2);

        await _deviceService.DeleteAllDevicesAsync(usuarioId);

        _dispositivoRepo.Verify(r => r.DeleteAllByUsuarioIdAsync(usuarioId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAllDevicesAsync_ClearsLegacyFcmToken()
    {
        var usuarioId = Guid.NewGuid();
        var usuario = LegacyUser(usuarioId, "legacy-token");
        _dispositivoRepo.Setup(r => r.DeleteAllByUsuarioIdAsync(usuarioId, It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);

        await _deviceService.DeleteAllDevicesAsync(usuarioId);

        Assert.Null(usuario.FcmToken);
        _usuarioRepo.Verify(r => r.UpdateAsync(usuario), Times.Once);
    }
}
