using ImpactX.Core.Domain;
using ImpactX.Core.Exceptions;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Models.DTOs;
using ImpactX.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace ImpactX.Tests.Unit;

public class SettingsServiceTests
{
    private readonly Mock<IUsuarioRepository> _usuarioRepo;
    private readonly SettingsService _settingsService;

    public SettingsServiceTests()
    {
        _usuarioRepo = new Mock<IUsuarioRepository>();
        var logger = Mock.Of<ILogger<SettingsService>>();
        _settingsService = new SettingsService(_usuarioRepo.Object, logger);
    }

    [Fact]
    public async Task GetSettingsAsync_WithPreferences_ReturnsDto()
    {
        var usuarioId = Guid.NewGuid();
        var usuario = new Usuario
        {
            Id = usuarioId,
            Preferencias = new PreferenciasUsuario
            {
                Idioma = "es",
                UnidadVelocidad = "kmh",
                NotificacionesPush = false,
                NotificacionesEmail = true,
                CompartirUbicacion = false,
            },
            Settings = new SettingsUsuario { TwoFactorEnabled = true },
        };

        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);

        var result = await _settingsService.GetSettingsAsync(usuarioId);

        Assert.Equal("es", result.Idioma);
        Assert.Equal("kmh", result.UnidadVelocidad);
        Assert.False(result.NotificacionesPush);
        Assert.True(result.NotificacionesEmail);
        Assert.False(result.CompartirUbicacion);
        Assert.True(result.TwoFactorEnabled);
    }

    [Fact]
    public async Task GetSettingsAsync_WithoutPreferences_ReturnsDefaults()
    {
        var usuarioId = Guid.NewGuid();
        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(new Usuario { Id = usuarioId });

        var result = await _settingsService.GetSettingsAsync(usuarioId);

        Assert.Null(result.Idioma);
        Assert.True(result.NotificacionesPush);
        Assert.True(result.NotificacionesEmail);
        Assert.True(result.CompartirUbicacion);
        Assert.False(result.TwoFactorEnabled);
    }

    [Fact]
    public async Task GetSettingsAsync_NonExistentUser_Throws()
    {
        _usuarioRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Usuario?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _settingsService.GetSettingsAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task UpdateSettingsAsync_UpdatesAllFields()
    {
        var usuarioId = Guid.NewGuid();
        var usuario = new Usuario { Id = usuarioId };

        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);

        var result = await _settingsService.UpdateSettingsAsync(usuarioId, new UpdateSettingsRequest
        {
            Idioma = "en",
            UnidadVelocidad = "mph",
            NotificacionesPush = false,
            NotificacionesEmail = false,
            CompartirUbicacion = false,
        });

        Assert.Equal("en", result.Idioma);
        Assert.Equal("mph", result.UnidadVelocidad);
        Assert.False(result.NotificacionesPush);
        Assert.False(result.NotificacionesEmail);
        Assert.False(result.CompartirUbicacion);
        _usuarioRepo.Verify(r => r.UpdateAsync(usuario), Times.Once);
    }

    [Fact]
    public async Task UpdateSettingsAsync_PartialUpdate_KeepsExisting()
    {
        var usuarioId = Guid.NewGuid();
        var usuario = new Usuario
        {
            Id = usuarioId,
            Preferencias = new PreferenciasUsuario
            {
                Idioma = "es",
                UnidadVelocidad = "kmh",
                NotificacionesPush = true,
                NotificacionesEmail = true,
                CompartirUbicacion = true,
            },
        };

        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);

        var result = await _settingsService.UpdateSettingsAsync(usuarioId, new UpdateSettingsRequest
        {
            Idioma = "fr",
        });

        Assert.Equal("fr", result.Idioma);
        Assert.Equal("kmh", result.UnidadVelocidad);
        Assert.True(result.NotificacionesPush);
    }

    [Fact]
    public async Task Setup2FaAsync_WithNoSecret_GeneratesSecretAndReturnsUri()
    {
        var usuarioId = Guid.NewGuid();
        var usuario = new Usuario
        {
            Id = usuarioId,
            Correo = "user@test.com",
        };

        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);

        var result = await _settingsService.Setup2FaAsync(usuarioId);

        Assert.NotEmpty(result.Secret);
        Assert.NotEmpty(result.QrCodeUri);
        Assert.NotEmpty(result.ManualKey);
        Assert.Contains("otpauth://totp/", result.QrCodeUri);
        Assert.Contains("secret=", result.QrCodeUri);
        Assert.Contains("ImpactX", result.QrCodeUri);
        _usuarioRepo.Verify(r => r.UpdateAsync(usuario), Times.Once);
    }

    [Fact]
    public async Task Setup2FaAsync_WhenAlreadyEnabled_Throws()
    {
        var usuarioId = Guid.NewGuid();
        var usuario = new Usuario
        {
            Id = usuarioId,
            Settings = new SettingsUsuario { TwoFactorEnabled = true },
        };

        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _settingsService.Setup2FaAsync(usuarioId));
    }

    [Fact]
    public async Task Enable2FaAsync_WithValidCode_Enables()
    {
        var usuarioId = Guid.NewGuid();
        var usuario = new Usuario
        {
            Id = usuarioId,
            Settings = new SettingsUsuario { TwoFactorSecret = "JBSWY3DPEHPK3PXP" },
        };

        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);

        var code = GenerateValidTotpCode("JBSWY3DPEHPK3PXP");

        await _settingsService.Enable2FaAsync(usuarioId, new Enable2FaRequest { Code = code });

        Assert.True(usuario.Settings.TwoFactorEnabled);
        Assert.NotNull(usuario.Settings.TwoFactorVerifiedAt);
        _usuarioRepo.Verify(r => r.UpdateAsync(usuario), Times.Once);
    }

    [Fact]
    public async Task Enable2FaAsync_WithoutSetup_Throws()
    {
        var usuarioId = Guid.NewGuid();
        var usuario = new Usuario { Id = usuarioId };

        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _settingsService.Enable2FaAsync(usuarioId, new Enable2FaRequest { Code = "123456" }));
    }

    [Fact]
    public async Task Enable2FaAsync_WhenAlreadyEnabled_Throws()
    {
        var usuarioId = Guid.NewGuid();
        var usuario = new Usuario
        {
            Id = usuarioId,
            Settings = new SettingsUsuario { TwoFactorEnabled = true, TwoFactorSecret = "secret" },
        };

        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _settingsService.Enable2FaAsync(usuarioId, new Enable2FaRequest { Code = "123456" }));
    }

    [Fact]
    public async Task Enable2FaAsync_WithInvalidCode_Throws()
    {
        var usuarioId = Guid.NewGuid();
        var usuario = new Usuario
        {
            Id = usuarioId,
            Settings = new SettingsUsuario { TwoFactorSecret = "JBSWY3DPEHPK3PXP" },
        };

        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            _settingsService.Enable2FaAsync(usuarioId, new Enable2FaRequest { Code = "000000" }));
    }

    [Fact]
    public async Task Disable2FaAsync_WithValidCode_Disables()
    {
        var usuarioId = Guid.NewGuid();
        var usuario = new Usuario
        {
            Id = usuarioId,
            Settings = new SettingsUsuario
            {
                TwoFactorEnabled = true,
                TwoFactorSecret = "JBSWY3DPEHPK3PXP",
                TwoFactorVerifiedAt = DateTime.UtcNow,
            },
        };

        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);

        var code = GenerateValidTotpCode("JBSWY3DPEHPK3PXP");

        await _settingsService.Disable2FaAsync(usuarioId, new Disable2FaRequest { Code = code });

        Assert.False(usuario.Settings.TwoFactorEnabled);
        Assert.Null(usuario.Settings.TwoFactorSecret);
        Assert.Null(usuario.Settings.TwoFactorVerifiedAt);
        _usuarioRepo.Verify(r => r.UpdateAsync(usuario), Times.Once);
    }

    [Fact]
    public async Task Disable2FaAsync_WhenNotEnabled_Throws()
    {
        var usuarioId = Guid.NewGuid();
        var usuario = new Usuario { Id = usuarioId };

        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _settingsService.Disable2FaAsync(usuarioId, new Disable2FaRequest { Code = "123456" }));
    }

    [Fact]
    public async Task Disable2FaAsync_WithInvalidCode_Throws()
    {
        var usuarioId = Guid.NewGuid();
        var usuario = new Usuario
        {
            Id = usuarioId,
            Settings = new SettingsUsuario { TwoFactorEnabled = true, TwoFactorSecret = "JBSWY3DPEHPK3PXP" },
        };

        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            _settingsService.Disable2FaAsync(usuarioId, new Disable2FaRequest { Code = "000000" }));
    }

    private static string GenerateValidTotpCode(string secret)
    {
        var secretBytes = SettingsService.FromBase32(secret);
        var timeCounter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        return ComputeTotpCode(secretBytes, timeCounter);
    }

    private static string ComputeTotpCode(byte[] secretBytes, long timeCounter)
    {
        var timeBytes = BitConverter.GetBytes(timeCounter);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(timeBytes);

        using var hmac = new System.Security.Cryptography.HMACSHA1(secretBytes);
        var hash = hmac.ComputeHash(timeBytes);

        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24) |
                     ((hash[offset + 1] & 0xFF) << 16) |
                     ((hash[offset + 2] & 0xFF) << 8) |
                     (hash[offset + 3] & 0xFF);

        var otp = binary % 1_000_000;
        return otp.ToString("D6");
    }
}
