using Moq;
using Prueba1.Core.Domain;
using Prueba1.Core.Interfaces.Repositories;
using Prueba1.Core.Interfaces.Services;
using Prueba1.Models.DTOs;
using Prueba1.Services;

namespace Prueba1.Tests.Unit;

public class AuthServiceTests
{
    private readonly Mock<IUsuarioRepository> _usuarioRepo;
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepo;
    private readonly Mock<IPasswordResetTokenRepository> _passwordResetTokenRepo;
    private readonly Mock<IEncryptionService> _encryptionService;
    private readonly Mock<ITokenService> _tokenService;
    private readonly Mock<IEmailService> _emailService;
    private readonly Mock<IPlanRepository> _planRepo;
    private readonly Mock<ISuscripcionRepository> _suscripcionRepo;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        _usuarioRepo = new Mock<IUsuarioRepository>();
        _refreshTokenRepo = new Mock<IRefreshTokenRepository>();
        _passwordResetTokenRepo = new Mock<IPasswordResetTokenRepository>();
        _encryptionService = new Mock<IEncryptionService>();
        _tokenService = new Mock<ITokenService>();
        _emailService = new Mock<IEmailService>();
        _planRepo = new Mock<IPlanRepository>();
        _suscripcionRepo = new Mock<ISuscripcionRepository>();

        _authService = new AuthService(
            _usuarioRepo.Object,
            _refreshTokenRepo.Object,
            _passwordResetTokenRepo.Object,
            _encryptionService.Object,
            _tokenService.Object,
            _emailService.Object,
            _planRepo.Object,
            _suscripcionRepo.Object);
    }

    [Fact]
    public async Task RegisterAsync_WithNewEmail_ReturnsSuccess()
    {
        _usuarioRepo.Setup(r => r.ExistsByCorreoAsync("test@test.com")).ReturnsAsync(false);
        _tokenService.Setup(t => t.GenerateAccessToken(It.IsAny<Usuario>())).Returns("access-token");
        _tokenService.Setup(t => t.GenerateRefreshToken()).Returns("refresh-token");

        var result = await _authService.RegisterAsync(new RegisterRequest
        {
            Nombre = "Test",
            Correo = "test@test.com",
            Password = "password123"
        });

        Assert.True(result.Success);
        Assert.Equal("Registro exitoso.", result.Mensaje);
        Assert.Equal("access-token", result.Token);
        Assert.Equal("refresh-token", result.RefreshToken);
        _usuarioRepo.Verify(r => r.AddAsync(It.IsAny<Usuario>()), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_WithExistingEmail_ReturnsConflict()
    {
        _usuarioRepo.Setup(r => r.ExistsByCorreoAsync("existing@test.com")).ReturnsAsync(true);

        var result = await _authService.RegisterAsync(new RegisterRequest
        {
            Nombre = "Test",
            Correo = "existing@test.com",
            Password = "password123"
        });

        Assert.False(result.Success);
        Assert.Equal("El correo ya está registrado.", result.Mensaje);
        _usuarioRepo.Verify(r => r.AddAsync(It.IsAny<Usuario>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsSuccess()
    {
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Nombre = "Test",
            Correo = "test@test.com",
            PasswordHash = "hashed",
            IsActive = true
        };

        _usuarioRepo.Setup(r => r.GetByCorreoAsync("test@test.com")).ReturnsAsync(usuario);
        _encryptionService.Setup(e => e.VerifyPassword("password", "hashed")).Returns(true);
        _tokenService.Setup(t => t.GenerateAccessToken(usuario)).Returns("access-token");
        _tokenService.Setup(t => t.GenerateRefreshToken()).Returns("refresh-token");

        var result = await _authService.LoginAsync(new LoginRequest
        {
            Correo = "test@test.com",
            Password = "password"
        });

        Assert.True(result.Success);
        Assert.Equal("Inicio de sesión exitoso.", result.Mensaje);
    }

    [Fact]
    public async Task LoginAsync_WithInvalidPassword_ReturnsUnauthorized()
    {
        var usuario = new Usuario
        {
            Correo = "test@test.com",
            PasswordHash = "hashed",
            IsActive = true
        };

        _usuarioRepo.Setup(r => r.GetByCorreoAsync("test@test.com")).ReturnsAsync(usuario);
        _encryptionService.Setup(e => e.VerifyPassword("wrong", "hashed")).Returns(false);

        var result = await _authService.LoginAsync(new LoginRequest
        {
            Correo = "test@test.com",
            Password = "wrong"
        });

        Assert.False(result.Success);
        Assert.Equal("Credenciales inválidas.", result.Mensaje);
    }

    [Fact]
    public async Task LoginAsync_WithInactiveAccount_ReturnsUnauthorized()
    {
        var usuario = new Usuario
        {
            Correo = "test@test.com",
            PasswordHash = "hashed",
            IsActive = false
        };

        _usuarioRepo.Setup(r => r.GetByCorreoAsync("test@test.com")).ReturnsAsync(usuario);
        _encryptionService.Setup(e => e.VerifyPassword("password", "hashed")).Returns(true);

        var result = await _authService.LoginAsync(new LoginRequest
        {
            Correo = "test@test.com",
            Password = "password"
        });

        Assert.False(result.Success);
        Assert.Equal("La cuenta está desactivada.", result.Mensaje);
    }

    [Fact]
    public async Task ChangePasswordAsync_WithCorrectCurrentPassword_ReturnsSuccess()
    {
        var usuarioId = Guid.NewGuid();
        var usuario = new Usuario
        {
            Id = usuarioId,
            PasswordHash = "current-hash"
        };

        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);
        _encryptionService.Setup(e => e.VerifyPassword("current", "current-hash")).Returns(true);
        _encryptionService.Setup(e => e.HashPassword("new-password")).Returns("new-hash");

        var result = await _authService.ChangePasswordAsync(usuarioId, new ChangePasswordRequest
        {
            CurrentPassword = "current",
            NewPassword = "new-password"
        });

        Assert.True(result.Success);
        Assert.Equal("Contraseña cambiada exitosamente.", result.Mensaje);
        Assert.Equal("new-hash", usuario.PasswordHash);
        _usuarioRepo.Verify(r => r.UpdateAsync(usuario), Times.Once);
    }

    [Fact]
    public async Task ChangePasswordAsync_WithWrongCurrentPassword_ReturnsError()
    {
        var usuarioId = Guid.NewGuid();
        var usuario = new Usuario
        {
            Id = usuarioId,
            PasswordHash = "current-hash"
        };

        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);
        _encryptionService.Setup(e => e.VerifyPassword("wrong", "current-hash")).Returns(false);

        var result = await _authService.ChangePasswordAsync(usuarioId, new ChangePasswordRequest
        {
            CurrentPassword = "wrong",
            NewPassword = "new-password"
        });

        Assert.False(result.Success);
        Assert.Equal("La contraseña actual es incorrecta.", result.Mensaje);
    }

    [Fact]
    public async Task RecoverPasswordAsync_WithExistingEmail_ReturnsSuccess()
    {
        var usuario = new Usuario { Id = Guid.NewGuid(), Correo = "test@test.com" };
        _usuarioRepo.Setup(r => r.GetByCorreoAsync("test@test.com")).ReturnsAsync(usuario);
        _tokenService.Setup(t => t.GeneratePasswordResetToken()).Returns("reset-token");

        var result = await _authService.RecoverPasswordAsync(new RecoverPasswordRequest
        {
            Correo = "test@test.com"
        });

        Assert.True(result.Success);
        Assert.Equal("reset-token", result.ResetToken);
        _passwordResetTokenRepo.Verify(r => r.AddAsync(It.IsAny<PasswordResetToken>()), Times.Once);
        _emailService.Verify(e => e.SendPasswordResetEmailAsync("test@test.com", "reset-token"), Times.Once);
    }

    [Fact]
    public async Task RecoverPasswordAsync_WithNonExistentEmail_ReturnsGenericMessage()
    {
        _usuarioRepo.Setup(r => r.GetByCorreoAsync("unknown@test.com")).ReturnsAsync((Usuario?)null);

        var result = await _authService.RecoverPasswordAsync(new RecoverPasswordRequest
        {
            Correo = "unknown@test.com"
        });

        Assert.True(result.Success);
        Assert.Equal("Si el correo existe, recibirás un enlace de recuperación.", result.Mensaje);
    }

    [Fact]
    public async Task ResetPasswordAsync_WithValidToken_ReturnsSuccess()
    {
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            PasswordHash = "old-hash"
        };
        var resetToken = new PasswordResetToken
        {
            Token = "valid-token",
            UsuarioId = usuario.Id,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        _passwordResetTokenRepo.Setup(r => r.GetByTokenAsync("valid-token")).ReturnsAsync(resetToken);
        _usuarioRepo.Setup(r => r.GetByIdAsync(usuario.Id)).ReturnsAsync(usuario);
        _encryptionService.Setup(e => e.HashPassword("new-password")).Returns("new-hash");

        var result = await _authService.ResetPasswordAsync(new ResetPasswordRequest
        {
            Token = "valid-token",
            NewPassword = "new-password"
        });

        Assert.True(result.Success);
        Assert.Equal("Contraseña restablecida exitosamente.", result.Mensaje);
        Assert.NotNull(resetToken.UsedAt);
        _usuarioRepo.Verify(r => r.UpdateAsync(usuario), Times.Once);
    }

    [Fact]
    public async Task ResetPasswordAsync_WithExpiredToken_ReturnsError()
    {
        var resetToken = new PasswordResetToken
        {
            Token = "expired-token",
            ExpiresAt = DateTime.UtcNow.AddHours(-1)
        };

        _passwordResetTokenRepo.Setup(r => r.GetByTokenAsync("expired-token")).ReturnsAsync(resetToken);

        var result = await _authService.ResetPasswordAsync(new ResetPasswordRequest
        {
            Token = "expired-token",
            NewPassword = "new-password"
        });

        Assert.False(result.Success);
        Assert.Equal("El token de recuperación es inválido o ha expirado.", result.Mensaje);
    }

    [Fact]
    public async Task LogoutAsync_RevokesToken()
    {
        var usuarioId = Guid.NewGuid();
        var token = new RefreshToken
        {
            Token = "refresh-token",
            UsuarioId = usuarioId
        };

        _refreshTokenRepo.Setup(r => r.GetByTokenAsync("refresh-token")).ReturnsAsync(token);

        var result = await _authService.LogoutAsync(usuarioId, "refresh-token");

        Assert.True(result.Success);
        Assert.NotNull(token.RevokedAt);
        _refreshTokenRepo.Verify(r => r.UpdateAsync(token), Times.Once);
    }

    [Fact]
    public async Task GetSessionsAsync_ReturnsActiveSessions()
    {
        var usuarioId = Guid.NewGuid();
        var tokens = new List<RefreshToken>
        {
            new()
            {
                Id = Guid.NewGuid(),
                DeviceInfo = "iPhone",
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                ExpiresAt = DateTime.UtcNow.AddDays(6)
            },
            new()
            {
                Id = Guid.NewGuid(),
                DeviceInfo = "Android",
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            }
        };

        _refreshTokenRepo.Setup(r => r.GetActiveByUserAsync(usuarioId)).ReturnsAsync(tokens);

        var sessions = await _authService.GetSessionsAsync(usuarioId);

        Assert.Equal(2, sessions.Count);
        Assert.Equal("iPhone", sessions[0].DeviceInfo);
        Assert.Equal("Android", sessions[1].DeviceInfo);
    }

    [Fact]
    public async Task DeleteAccountAsync_MarksUserAsInactive()
    {
        var usuarioId = Guid.NewGuid();
        var usuario = new Usuario { Id = usuarioId, IsActive = true };

        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);

        await _authService.DeleteAccountAsync(usuarioId);

        Assert.False(usuario.IsActive);
        _usuarioRepo.Verify(r => r.UpdateAsync(usuario), Times.Once);
    }

    [Fact]
    public async Task ExportAccountAsync_ReturnsUserData()
    {
        var usuarioId = Guid.NewGuid();
        var usuario = new Usuario
        {
            Id = usuarioId,
            Nombre = "Test User",
            Correo = "test@test.com",
            Telefono = "123456789",
            PlanActivo = "Premium",
            CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            LastLoginAt = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            EmailConfirmed = true
        };

        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);

        var export = await _authService.ExportAccountAsync(usuarioId);

        Assert.Equal("Test User", export.Nombre);
        Assert.Equal("test@test.com", export.Correo);
        Assert.Equal("Premium", export.PlanActivo);
        Assert.True(export.EmailConfirmed);
    }
}
