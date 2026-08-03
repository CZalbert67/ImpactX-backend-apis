using Moq;
using ImpactX.Core.Domain;
using ImpactX.Core.Exceptions;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Core.Interfaces.Services;
using ImpactX.Core.Security;
using ImpactX.Models.DTOs;
using ImpactX.Services;
using Microsoft.Azure.Cosmos;

namespace ImpactX.Tests.Unit;

public class AuthServiceTests
{
    private readonly Mock<IUsuarioRepository> _usuarioRepo;
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepo;
    private readonly Mock<IPasswordResetTokenRepository> _passwordResetTokenRepo;
    private readonly Mock<IDispositivoRepository> _dispositivoRepo;
    private readonly Mock<IEncryptionService> _encryptionService;
    private readonly Mock<ITokenService> _tokenService;
    private readonly Mock<IEmailService> _emailService;
    private readonly Mock<IPlanRepository> _planRepo;
    private readonly Mock<ISuscripcionRepository> _suscripcionRepo;
    private readonly ListLogger<AuthService> _logger;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        _usuarioRepo = new Mock<IUsuarioRepository>();
        _refreshTokenRepo = new Mock<IRefreshTokenRepository>();
        _passwordResetTokenRepo = new Mock<IPasswordResetTokenRepository>();
        _dispositivoRepo = new Mock<IDispositivoRepository>();
        _encryptionService = new Mock<IEncryptionService>();
        _tokenService = new Mock<ITokenService>();
        _emailService = new Mock<IEmailService>();
        _planRepo = new Mock<IPlanRepository>();
        _suscripcionRepo = new Mock<ISuscripcionRepository>();
        _logger = new ListLogger<AuthService>();

        _authService = new AuthService(
            _usuarioRepo.Object,
            _refreshTokenRepo.Object,
            _passwordResetTokenRepo.Object,
            _dispositivoRepo.Object,
            _encryptionService.Object,
            _tokenService.Object,
            _emailService.Object,
            _planRepo.Object,
            _suscripcionRepo.Object,
            _logger);
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

    [Theory]
    [InlineData("Juan puta Lopez")]
    [InlineData("juan maricon")]
    [InlineData("PUTA")]
    public async Task RegisterAsync_OffensiveName_ThrowsBadRequest(string nombre)
    {
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            _authService.RegisterAsync(new RegisterRequest
            {
                Nombre = nombre,
                Correo = "offensive@test.com",
                Password = "password123"
            }));

        Assert.Equal("El nombre contiene palabras inapropiadas.", exception.Message);
        _usuarioRepo.Verify(r => r.ExistsByCorreoAsync(It.IsAny<string>()), Times.Never);
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
    [Trait("Category", "Security")]
    public async Task RecoverPasswordAsync_WithExistingEmail_ReturnsGenericResponse()
    {
        var usuario = new Usuario { Id = Guid.NewGuid(), Correo = "test@test.com" };
        _usuarioRepo.Setup(r => r.GetByCorreoAsync("test@test.com")).ReturnsAsync(usuario);
        _tokenService.Setup(t => t.GeneratePasswordResetToken()).Returns("reset-token");

        var result = await _authService.RecoverPasswordAsync(new RecoverPasswordRequest
        {
            Correo = "test@test.com"
        });

        Assert.True(result.Success);
        Assert.Equal("Si la cuenta existe, se enviaron instrucciones para restablecer la contraseña.", result.Mensaje);
        _passwordResetTokenRepo.Verify(r => r.AddAsync(It.IsAny<PasswordResetToken>()), Times.Once);
        _emailService.Verify(e => e.SendPasswordResetEmailAsync("test@test.com", "reset-token"), Times.Once);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task RecoverPasswordAsync_WithNonExistentEmail_ReturnsSameGenericResponse()
    {
        _usuarioRepo.Setup(r => r.GetByCorreoAsync("unknown@test.com")).ReturnsAsync((Usuario?)null);

        var result = await _authService.RecoverPasswordAsync(new RecoverPasswordRequest
        {
            Correo = "unknown@test.com"
        });

        Assert.True(result.Success);
        Assert.Equal("Si la cuenta existe, se enviaron instrucciones para restablecer la contraseña.", result.Mensaje);
        _emailService.Verify(e => e.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
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
            TokenHash = PasswordResetTokenHasher.Hash("valid-token"),
            UsuarioId = usuario.Id,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        _passwordResetTokenRepo.Setup(r => r.GetByTokenHashAsync(resetToken.TokenHash)).ReturnsAsync(resetToken);
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
            TokenHash = PasswordResetTokenHasher.Hash("expired-token"),
            ExpiresAt = DateTime.UtcNow.AddHours(-1)
        };

        _passwordResetTokenRepo.Setup(r => r.GetByTokenHashAsync(resetToken.TokenHash)).ReturnsAsync(resetToken);

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

    [Fact]
    [Trait("Category", "Security")]
    public async Task RefreshTokenAsync_WithValidToken_ReturnsSuccess()
    {
        var usuarioId = Guid.NewGuid();
        var usuario = new Usuario { Id = usuarioId, IsActive = true, Nombre = "Test", Correo = "test@test.com" };
        var refreshToken = new RefreshToken
        {
            Token = "valid-refresh-token",
            UsuarioId = usuarioId,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        _refreshTokenRepo.Setup(r => r.GetByTokenAsync("valid-refresh-token")).ReturnsAsync(refreshToken);
        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);
        _tokenService.Setup(t => t.GenerateAccessToken(usuario)).Returns("new-access-token");
        _tokenService.Setup(t => t.GenerateRefreshToken()).Returns("new-refresh-token");

        var result = await _authService.RefreshTokenAsync(new RefreshTokenRequest
        {
            RefreshToken = "valid-refresh-token"
        });

        Assert.True(result.Success);
        Assert.Equal("Sesión renovada exitosamente.", result.Mensaje);
        Assert.Equal("new-access-token", result.Token);
        Assert.Equal("new-refresh-token", result.RefreshToken);
        Assert.NotNull(refreshToken.RevokedAt);
        _refreshTokenRepo.Verify(r => r.UpdateAsync(refreshToken), Times.Once);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task RefreshTokenAsync_WithNullRequest_ReturnsUnauthorized()
    {
        var result = await _authService.RefreshTokenAsync(null!);

        Assert.False(result.Success);
        Assert.Equal("Autenticación fallida.", result.Mensaje);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task RefreshTokenAsync_WithEmptyToken_ReturnsUnauthorized()
    {
        var result = await _authService.RefreshTokenAsync(new RefreshTokenRequest
        {
            RefreshToken = string.Empty
        });

        Assert.False(result.Success);
        Assert.Equal("Autenticación fallida.", result.Mensaje);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task RefreshTokenAsync_WithNonExistentToken_ReturnsUnauthorized()
    {
        _refreshTokenRepo.Setup(r => r.GetByTokenAsync("invalid-token")).ReturnsAsync((RefreshToken?)null);

        var result = await _authService.RefreshTokenAsync(new RefreshTokenRequest
        {
            RefreshToken = "invalid-token"
        });

        Assert.False(result.Success);
        Assert.Equal("Autenticación fallida.", result.Mensaje);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task RefreshTokenAsync_WithExpiredToken_ReturnsUnauthorized()
    {
        var expiredToken = new RefreshToken
        {
            Token = "expired-token",
            UsuarioId = Guid.NewGuid(),
            ExpiresAt = DateTime.UtcNow.AddHours(-1)
        };

        _refreshTokenRepo.Setup(r => r.GetByTokenAsync("expired-token")).ReturnsAsync(expiredToken);

        var result = await _authService.RefreshTokenAsync(new RefreshTokenRequest
        {
            RefreshToken = "expired-token"
        });

        Assert.False(result.Success);
        Assert.Equal("Autenticación fallida.", result.Mensaje);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task RefreshTokenAsync_WithRevokedToken_ReturnsUnauthorized()
    {
        var revokedToken = new RefreshToken
        {
            Token = "revoked-token",
            UsuarioId = Guid.NewGuid(),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            RevokedAt = DateTime.UtcNow.AddHours(-1)
        };

        _refreshTokenRepo.Setup(r => r.GetByTokenAsync("revoked-token")).ReturnsAsync(revokedToken);

        var result = await _authService.RefreshTokenAsync(new RefreshTokenRequest
        {
            RefreshToken = "revoked-token"
        });

        Assert.False(result.Success);
        Assert.Equal("Autenticación fallida.", result.Mensaje);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task RefreshTokenAsync_WithNonExistentUser_ReturnsUnauthorized()
    {
        var usuarioId = Guid.NewGuid();
        var refreshToken = new RefreshToken
        {
            Token = "orphan-token",
            UsuarioId = usuarioId,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        _refreshTokenRepo.Setup(r => r.GetByTokenAsync("orphan-token")).ReturnsAsync(refreshToken);
        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync((Usuario?)null);

        var result = await _authService.RefreshTokenAsync(new RefreshTokenRequest
        {
            RefreshToken = "orphan-token"
        });

        Assert.False(result.Success);
        Assert.Equal("Autenticación fallida.", result.Mensaje);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task RefreshTokenAsync_WithInactiveUser_ReturnsUnauthorized()
    {
        var usuarioId = Guid.NewGuid();
        var usuario = new Usuario { Id = usuarioId, IsActive = false };
        var refreshToken = new RefreshToken
        {
            Token = "inactive-user-token",
            UsuarioId = usuarioId,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        _refreshTokenRepo.Setup(r => r.GetByTokenAsync("inactive-user-token")).ReturnsAsync(refreshToken);
        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);

        var result = await _authService.RefreshTokenAsync(new RefreshTokenRequest
        {
            RefreshToken = "inactive-user-token"
        });

        Assert.False(result.Success);
        Assert.Equal("Autenticación fallida.", result.Mensaje);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task RefreshTokenAsync_WhenValidationFails_DoesNotGenerateToken()
    {
        _refreshTokenRepo.Setup(r => r.GetByTokenAsync("invalid")).ReturnsAsync((RefreshToken?)null);

        var result = await _authService.RefreshTokenAsync(new RefreshTokenRequest
        {
            RefreshToken = "invalid"
        });

        Assert.False(result.Success);
        _tokenService.Verify(t => t.GenerateAccessToken(It.IsAny<Usuario>()), Times.Never);
        _tokenService.Verify(t => t.GenerateRefreshToken(), Times.Never);
        _refreshTokenRepo.Verify(r => r.AddAsync(It.IsAny<RefreshToken>()), Times.Never);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task RecoverPasswordResponse_DoesNotContainResetToken()
    {
        var usuario = new Usuario { Id = Guid.NewGuid(), Correo = "test@test.com" };
        _usuarioRepo.Setup(r => r.GetByCorreoAsync("test@test.com")).ReturnsAsync(usuario);
        _tokenService.Setup(t => t.GeneratePasswordResetToken()).Returns("reset-token");

        var result = await _authService.RecoverPasswordAsync(new RecoverPasswordRequest
        {
            Correo = "test@test.com"
        });

        Assert.IsType<RecoverPasswordResponse>(result);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task RecoverPasswordAsync_DoesNotRevealUserExistence()
    {
        var usuario = new Usuario { Id = Guid.NewGuid(), Correo = "test@test.com" };
        _usuarioRepo.Setup(r => r.GetByCorreoAsync("test@test.com")).ReturnsAsync(usuario);
        _tokenService.Setup(t => t.GeneratePasswordResetToken()).Returns("reset-token");

        var existingResult = await _authService.RecoverPasswordAsync(new RecoverPasswordRequest
        {
            Correo = "test@test.com"
        });

        _usuarioRepo.Setup(r => r.GetByCorreoAsync("unknown@test.com")).ReturnsAsync((Usuario?)null);

        var missingResult = await _authService.RecoverPasswordAsync(new RecoverPasswordRequest
        {
            Correo = "unknown@test.com"
        });

        Assert.Equal(existingResult.Mensaje, missingResult.Mensaje);
        Assert.Equal(existingResult.Success, missingResult.Success);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task ResetPasswordAsync_WithUsedToken_ReturnsError()
    {
        var resetToken = new PasswordResetToken
        {
            TokenHash = PasswordResetTokenHasher.Hash("used-token"),
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            UsedAt = DateTime.UtcNow.AddHours(-1)
        };

        _passwordResetTokenRepo.Setup(r => r.GetByTokenHashAsync(resetToken.TokenHash)).ReturnsAsync(resetToken);

        var result = await _authService.ResetPasswordAsync(new ResetPasswordRequest
        {
            Token = "used-token",
            NewPassword = "new-password"
        });

        Assert.False(result.Success);
        Assert.Equal("El token de recuperación es inválido o ha expirado.", result.Mensaje);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task ResetPasswordAsync_TokenCannotBeReused()
    {
        var usuarioId = Guid.NewGuid();
        var usuario = new Usuario { Id = usuarioId, PasswordHash = "old-hash" };
        var resetToken = new PasswordResetToken
        {
            TokenHash = PasswordResetTokenHasher.Hash("single-use-token"),
            UsuarioId = usuarioId,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        _passwordResetTokenRepo.Setup(r => r.GetByTokenHashAsync(resetToken.TokenHash)).ReturnsAsync(resetToken);
        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);
        _encryptionService.Setup(e => e.HashPassword("new-password")).Returns("new-hash");

        var firstResult = await _authService.ResetPasswordAsync(new ResetPasswordRequest
        {
            Token = "single-use-token",
            NewPassword = "new-password"
        });

        Assert.True(firstResult.Success);
        Assert.NotNull(resetToken.UsedAt);

        _passwordResetTokenRepo.Setup(r => r.GetByTokenHashAsync(resetToken.TokenHash)).ReturnsAsync(resetToken);

        var secondResult = await _authService.ResetPasswordAsync(new ResetPasswordRequest
        {
            Token = "single-use-token",
            NewPassword = "another-password"
        });

        Assert.False(secondResult.Success);
        Assert.Equal("El token de recuperación es inválido o ha expirado.", secondResult.Mensaje);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task ResetPasswordAsync_RevokesAllSessions()
    {
        var usuarioId = Guid.NewGuid();
        var usuario = new Usuario { Id = usuarioId, PasswordHash = "old-hash" };
        var resetToken = new PasswordResetToken
        {
            TokenHash = PasswordResetTokenHasher.Hash("reset-revoke-token"),
            UsuarioId = usuarioId,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        _passwordResetTokenRepo.Setup(r => r.GetByTokenHashAsync(resetToken.TokenHash)).ReturnsAsync(resetToken);
        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);
        _encryptionService.Setup(e => e.HashPassword("new-password")).Returns("new-hash");

        var result = await _authService.ResetPasswordAsync(new ResetPasswordRequest
        {
            Token = "reset-revoke-token",
            NewPassword = "new-password"
        });

        Assert.True(result.Success);
        _refreshTokenRepo.Verify(r => r.RevokeAllByUsuarioIdAsync(usuarioId, It.IsAny<DateTime>(), default), Times.Once);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task ChangePasswordAsync_RevokesAllSessions()
    {
        var usuarioId = Guid.NewGuid();
        var usuario = new Usuario
        {
            Id = usuarioId,
            PasswordHash = "current-hash"
        };

        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);
        _encryptionService.Setup(e => e.VerifyPassword("current", "current-hash")).Returns(true);

        var result = await _authService.ChangePasswordAsync(usuarioId, new ChangePasswordRequest
        {
            CurrentPassword = "current",
            NewPassword = "new-password"
        });

        Assert.True(result.Success);
        _refreshTokenRepo.Verify(r => r.RevokeAllByUsuarioIdAsync(usuarioId, It.IsAny<DateTime>(), default), Times.Once);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task ChangePasswordAsync_DoesNotAffectOtherUserSessions()
    {
        var usuarioId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var usuario = new Usuario
        {
            Id = usuarioId,
            PasswordHash = "current-hash"
        };

        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);
        _encryptionService.Setup(e => e.VerifyPassword("current", "current-hash")).Returns(true);

        var result = await _authService.ChangePasswordAsync(usuarioId, new ChangePasswordRequest
        {
            CurrentPassword = "current",
            NewPassword = "new-password"
        });

        Assert.True(result.Success);
        _refreshTokenRepo.Verify(r => r.RevokeAllByUsuarioIdAsync(usuarioId, It.IsAny<DateTime>(), default), Times.Once);
        _refreshTokenRepo.Verify(r => r.RevokeAllByUsuarioIdAsync(otherUserId, It.IsAny<DateTime>(), default), Times.Never);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task DeleteAccountAsync_RevokesAllSessions()
    {
        var usuarioId = Guid.NewGuid();
        var usuario = new Usuario { Id = usuarioId, IsActive = true };

        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);

        await _authService.DeleteAccountAsync(usuarioId);

        Assert.False(usuario.IsActive);
        _refreshTokenRepo.Verify(r => r.RevokeAllByUsuarioIdAsync(usuarioId, It.IsAny<DateTime>(), default), Times.Once);
        _dispositivoRepo.Verify(r => r.DeleteAllByUsuarioIdAsync(usuarioId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task DeleteAccountAsync_SecondRevocationDoesNotFail()
    {
        var usuarioId = Guid.NewGuid();
        var usuario = new Usuario { Id = usuarioId, IsActive = true };

        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);

        await _authService.DeleteAccountAsync(usuarioId);

        var mismoUsuario = new Usuario { Id = usuarioId, IsActive = false };
        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(mismoUsuario);

        await _authService.DeleteAccountAsync(usuarioId);

        _refreshTokenRepo.Verify(r => r.RevokeAllByUsuarioIdAsync(usuarioId, It.IsAny<DateTime>(), default), Times.Exactly(2));
        _dispositivoRepo.Verify(r => r.DeleteAllByUsuarioIdAsync(usuarioId, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task RecoverPasswordAsync_StoresOnlyTokenHash_NotPlainText()
    {
        var usuario = new Usuario { Id = Guid.NewGuid(), Correo = "hash@test.com" };
        PasswordResetToken? stored = null;

        _usuarioRepo.Setup(r => r.GetByCorreoAsync("hash@test.com")).ReturnsAsync(usuario);
        _tokenService.Setup(t => t.GeneratePasswordResetToken()).Returns("raw-reset-token");
        _passwordResetTokenRepo.Setup(r => r.AddAsync(It.IsAny<PasswordResetToken>()))
            .Callback<PasswordResetToken>(t => stored = t)
            .Returns(Task.CompletedTask);

        await _authService.RecoverPasswordAsync(new RecoverPasswordRequest
        {
            Correo = "hash@test.com"
        });

        Assert.NotNull(stored);
        Assert.Equal(PasswordResetTokenHasher.Hash("raw-reset-token"), stored!.TokenHash);
        Assert.DoesNotContain("raw-reset-token", stored.TokenHash);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task ResetPasswordAsync_WithWrongToken_ReturnsError()
    {
        _passwordResetTokenRepo.Setup(r => r.GetByTokenHashAsync(It.IsAny<string>()))
            .ReturnsAsync((PasswordResetToken?)null);

        var result = await _authService.ResetPasswordAsync(new ResetPasswordRequest
        {
            Token = "wrong-token",
            NewPassword = "new-password"
        });

        Assert.False(result.Success);
        Assert.Equal("El token de recuperación es inválido o ha expirado.", result.Mensaje);
        _passwordResetTokenRepo.Verify(r => r.GetByTokenHashAsync(PasswordResetTokenHasher.Hash("wrong-token")), Times.Once);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task RecoverPasswordAsync_SecondRequest_InvalidatesFirstToken()
    {
        var usuario = new Usuario { Id = Guid.NewGuid(), Correo = "invalidate@test.com" };

        _usuarioRepo.Setup(r => r.GetByCorreoAsync("invalidate@test.com")).ReturnsAsync(usuario);
        _tokenService.SetupSequence(t => t.GeneratePasswordResetToken())
            .Returns("first-token")
            .Returns("second-token");

        var sequence = new MockSequence();
        _passwordResetTokenRepo.InSequence(sequence)
            .Setup(r => r.InvalidateAllByUsuarioIdAsync(usuario.Id, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(0));
        _passwordResetTokenRepo.InSequence(sequence)
            .Setup(r => r.AddAsync(It.IsAny<PasswordResetToken>()))
            .Returns(Task.CompletedTask);
        _passwordResetTokenRepo.InSequence(sequence)
            .Setup(r => r.InvalidateAllByUsuarioIdAsync(usuario.Id, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(0));
        _passwordResetTokenRepo.InSequence(sequence)
            .Setup(r => r.AddAsync(It.IsAny<PasswordResetToken>()))
            .Returns(Task.CompletedTask);

        await _authService.RecoverPasswordAsync(new RecoverPasswordRequest { Correo = "invalidate@test.com" });
        await _authService.RecoverPasswordAsync(new RecoverPasswordRequest { Correo = "invalidate@test.com" });

        _passwordResetTokenRepo.Verify(r => r.InvalidateAllByUsuarioIdAsync(usuario.Id, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _passwordResetTokenRepo.Verify(r => r.AddAsync(It.IsAny<PasswordResetToken>()), Times.Exactly(2));
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task RecoverPasswordAsync_DoesNotLogTokenOrCorreo()
    {
        var usuario = new Usuario { Id = Guid.NewGuid(), Correo = "log-secret@test.com" };
        _usuarioRepo.Setup(r => r.GetByCorreoAsync("log-secret@test.com")).ReturnsAsync(usuario);
        _tokenService.Setup(t => t.GeneratePasswordResetToken()).Returns("super-secret-reset-token");
        _passwordResetTokenRepo.Setup(r => r.AddAsync(It.IsAny<PasswordResetToken>())).Returns(Task.CompletedTask);
        _passwordResetTokenRepo.Setup(r => r.InvalidateAllByUsuarioIdAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(0));

        await _authService.RecoverPasswordAsync(new RecoverPasswordRequest { Correo = "log-secret@test.com" });

        foreach (var entry in _logger.LogEntries)
        {
            Assert.DoesNotContain("super-secret-reset-token", entry, StringComparison.Ordinal);
            Assert.DoesNotContain("log-secret@test.com", entry, StringComparison.Ordinal);
            Assert.DoesNotContain(PasswordResetTokenHasher.Hash("super-secret-reset-token"), entry, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task ResetPasswordAsync_DoesNotLogTokenOrNewPassword()
    {
        var usuarioId = Guid.NewGuid();
        var usuario = new Usuario { Id = usuarioId, PasswordHash = "old-hash" };
        var resetToken = new PasswordResetToken
        {
            TokenHash = PasswordResetTokenHasher.Hash("log-token"),
            UsuarioId = usuarioId,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        _passwordResetTokenRepo.Setup(r => r.GetByTokenHashAsync(resetToken.TokenHash)).ReturnsAsync(resetToken);
        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);
        _encryptionService.Setup(e => e.HashPassword("log-new-password")).Returns("new-hash");

        await _authService.ResetPasswordAsync(new ResetPasswordRequest
        {
            Token = "log-token",
            NewPassword = "log-new-password"
        });

        foreach (var entry in _logger.LogEntries)
        {
            Assert.DoesNotContain("log-token", entry, StringComparison.Ordinal);
            Assert.DoesNotContain("log-new-password", entry, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task RegisterAsync_FreePlanConflict_DoesNotFailRegistration()
    {
        _usuarioRepo.Setup(r => r.ExistsByCorreoAsync("conflict@test.com")).ReturnsAsync(false);
        _usuarioRepo.Setup(r => r.ExistsByUsernameIncludingHistoryAsync(It.IsAny<string>())).ReturnsAsync(false);
        _planRepo.Setup(r => r.GetByNameAsync("Free")).ReturnsAsync(new Plan { Id = Guid.NewGuid(), Nombre = "Free" });
        _suscripcionRepo.Setup(r => r.AddAsync(It.IsAny<Suscripcion>()))
            .ThrowsAsync(new CosmosException("Conflict", System.Net.HttpStatusCode.Conflict, 409, string.Empty, 0));
        _tokenService.Setup(t => t.GenerateAccessToken(It.IsAny<Usuario>())).Returns("access-token");
        _tokenService.Setup(t => t.GenerateRefreshToken()).Returns("refresh-token");

        var result = await _authService.RegisterAsync(new RegisterRequest
        {
            Nombre = "Test",
            Correo = "conflict@test.com",
            Password = "password123"
        });

        Assert.True(result.Success);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task RegisterAsync_FreePlanUnexpectedError_Propagates()
    {
        _usuarioRepo.Setup(r => r.ExistsByCorreoAsync("unexpected@test.com")).ReturnsAsync(false);
        _usuarioRepo.Setup(r => r.ExistsByUsernameIncludingHistoryAsync(It.IsAny<string>())).ReturnsAsync(false);
        _planRepo.Setup(r => r.GetByNameAsync("Free")).ThrowsAsync(new InvalidOperationException("boom"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _authService.RegisterAsync(new RegisterRequest
        {
            Nombre = "Test",
            Correo = "unexpected@test.com",
            Password = "password123"
        }));
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task RegisterAsync_FreePlanCancellation_Propagates()
    {
        _usuarioRepo.Setup(r => r.ExistsByCorreoAsync("cancel@test.com")).ReturnsAsync(false);
        _usuarioRepo.Setup(r => r.ExistsByUsernameIncludingHistoryAsync(It.IsAny<string>())).ReturnsAsync(false);
        _planRepo.Setup(r => r.GetByNameAsync("Free")).ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() => _authService.RegisterAsync(new RegisterRequest
        {
            Nombre = "Test",
            Correo = "cancel@test.com",
            Password = "password123"
        }));
    }
}
