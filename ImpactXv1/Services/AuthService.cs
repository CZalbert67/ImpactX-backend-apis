using ImpactX.Core.Domain;
using ImpactX.Core.Exceptions;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Core.Interfaces.Services;
using ImpactX.Models.DTOs;

namespace ImpactX.Services;

public class AuthService : IAuthService
{
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordResetTokenRepository _passwordResetTokenRepository;
    private readonly IEncryptionService _encryptionService;
    private readonly ITokenService _tokenService;
    private readonly IEmailService _emailService;
    private readonly IPlanRepository _planRepository;
    private readonly ISuscripcionRepository _suscripcionRepository;

    public AuthService(
        IUsuarioRepository usuarioRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordResetTokenRepository passwordResetTokenRepository,
        IEncryptionService encryptionService,
        ITokenService tokenService,
        IEmailService emailService,
        IPlanRepository planRepository,
        ISuscripcionRepository suscripcionRepository)
    {
        _usuarioRepository = usuarioRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordResetTokenRepository = passwordResetTokenRepository;
        _encryptionService = encryptionService;
        _tokenService = tokenService;
        _emailService = emailService;
        _planRepository = planRepository;
        _suscripcionRepository = suscripcionRepository;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        if (await _usuarioRepository.ExistsByCorreoAsync(request.Correo))
        {
            return new AuthResponse
            {
                Success = false,
                Mensaje = "El correo ya está registrado."
            };
        }

        var username = GenerateUsername(request.Nombre);
        while (await _usuarioRepository.ExistsByUsernameAsync(username))
        {
            username = GenerateUsername(request.Nombre);
        }

        var usuario = new Usuario
        {
            Nombre = request.Nombre,
            Username = username,
            AppId = GenerateAppId(request.Nombre),
            InviteCode = GenerateInviteCode(request.Nombre),
            Correo = request.Correo,
            Telefono = request.Telefono ?? string.Empty,
            PasswordHash = _encryptionService.HashPassword(request.Password),
            PlanActivo = request.PlanActivo
        };

        await _usuarioRepository.AddAsync(usuario);

        try
        {
            var freePlan = await _planRepository.GetByNameAsync("Free");
            if (freePlan is not null)
            {
                var trialEnd = DateTime.UtcNow.AddDays(14);
                var suscripcion = new Suscripcion
                {
                    UsuarioId = usuario.Id,
                    PlanId = freePlan.Id,
                    Estado = "Trial",
                    Inicio = DateTime.UtcNow,
                    TrialFin = trialEnd,
                    Fin = trialEnd,
                };
                await _suscripcionRepository.AddAsync(suscripcion);
                usuario.PlanActivo = "Free";
                await _usuarioRepository.UpdateAsync(usuario);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AuthService] Info: Asignación de plan omitida ({ex.Message}).");
        }

        var accessToken = _tokenService.GenerateAccessToken(usuario);
        var refreshToken = await CreateRefreshTokenAsync(usuario);

        return CreateAuthResponse(usuario, accessToken, refreshToken, "Registro exitoso.");
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var usuario = await _usuarioRepository.GetByCorreoAsync(request.Correo);

        if (usuario is null || !_encryptionService.VerifyPassword(request.Password, usuario.PasswordHash))
        {
            return new AuthResponse
            {
                Success = false,
                Mensaje = "Credenciales inválidas."
            };
        }

        if (!usuario.IsActive)
        {
            return new AuthResponse
            {
                Success = false,
                Mensaje = "La cuenta está desactivada."
            };
        }

        usuario.LastLoginAt = DateTime.UtcNow;
        await _usuarioRepository.UpdateAsync(usuario);

        var accessToken = _tokenService.GenerateAccessToken(usuario);
        var refreshToken = await CreateRefreshTokenAsync(usuario);

        return CreateAuthResponse(usuario, accessToken, refreshToken, "Inicio de sesión exitoso.");
    }

    public async Task<AuthResponse> RecoverPasswordAsync(RecoverPasswordRequest request)
    {
        var usuario = await _usuarioRepository.GetByCorreoAsync(request.Correo);

        if (usuario is null)
        {
            return new AuthResponse
            {
                Success = true,
                Mensaje = "Si el correo existe, recibirás un enlace de recuperación."
            };
        }

        var token = _tokenService.GeneratePasswordResetToken();
        var resetToken = new PasswordResetToken
        {
            UsuarioId = usuario.Id,
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        await _passwordResetTokenRepository.AddAsync(resetToken);

        await _emailService.SendPasswordResetEmailAsync(usuario.Correo, token);

        return new AuthResponse
        {
            Success = true,
            Mensaje = "Si el correo existe, recibirás un enlace de recuperación.",
            ResetToken = token
        };
    }

    public async Task<AuthResponse> ResetPasswordAsync(ResetPasswordRequest request)
    {
        var resetToken = await _passwordResetTokenRepository.GetByTokenAsync(request.Token);

        if (resetToken is null || !resetToken.IsValid)
        {
            return new AuthResponse
            {
                Success = false,
                Mensaje = "El token de recuperación es inválido o ha expirado."
            };
        }

        resetToken.UsedAt = DateTime.UtcNow;
        await _passwordResetTokenRepository.UpdateAsync(resetToken);

        var usuario = await _usuarioRepository.GetByIdAsync(resetToken.UsuarioId);
        if (usuario is null)
        {
            return new AuthResponse
            {
                Success = false,
                Mensaje = "Usuario no encontrado."
            };
        }
        usuario.PasswordHash = _encryptionService.HashPassword(request.NewPassword);
        await _usuarioRepository.UpdateAsync(usuario);

        return new AuthResponse
        {
            Success = true,
            Mensaje = "Contraseña restablecida exitosamente."
        };
    }

    public async Task<AuthResponse> ChangePasswordAsync(Guid usuarioId, ChangePasswordRequest request)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(usuarioId);

        if (usuario is null)
        {
            return new AuthResponse
            {
                Success = false,
                Mensaje = "Usuario no encontrado."
            };
        }

        if (!_encryptionService.VerifyPassword(request.CurrentPassword, usuario.PasswordHash))
        {
            return new AuthResponse
            {
                Success = false,
                Mensaje = "La contraseña actual es incorrecta."
            };
        }

        usuario.PasswordHash = _encryptionService.HashPassword(request.NewPassword);
        await _usuarioRepository.UpdateAsync(usuario);

        return new AuthResponse
        {
            Success = true,
            Mensaje = "Contraseña cambiada exitosamente."
        };
    }

    public async Task<AuthResponse> LogoutAsync(Guid usuarioId, string refreshToken)
    {
        var token = await _refreshTokenRepository.GetByTokenAsync(refreshToken);

        if (token is not null && token.UsuarioId == usuarioId)
        {
            token.RevokedAt = DateTime.UtcNow;
            await _refreshTokenRepository.UpdateAsync(token);
        }

        return new AuthResponse
        {
            Success = true,
            Mensaje = "Sesión cerrada exitosamente."
        };
    }

    public async Task<List<SessionDto>> GetSessionsAsync(Guid usuarioId)
    {
        var tokens = await _refreshTokenRepository.GetActiveByUserAsync(usuarioId);

        return tokens.Select(t => new SessionDto
        {
            Id = t.Id,
            DeviceInfo = t.DeviceInfo,
            CreatedAt = t.CreatedAt,
            ExpiresAt = t.ExpiresAt,
            IsActive = t.IsActive
        }).ToList();
    }

    public async Task DeleteSessionAsync(Guid usuarioId, Guid sessionId)
    {
        var tokens = await _refreshTokenRepository.GetActiveByUserAsync(usuarioId);
        var token = tokens.FirstOrDefault(t => t.Id == sessionId);

        if (token is not null)
        {
            token.RevokedAt = DateTime.UtcNow;
            await _refreshTokenRepository.UpdateAsync(token);
        }
    }

    public async Task DeleteAccountAsync(Guid usuarioId)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(usuarioId);
        if (usuario is not null)
        {
            usuario.IsActive = false;
            await _usuarioRepository.UpdateAsync(usuario);
        }
    }

    public async Task<ExportAccountDto> ExportAccountAsync(Guid usuarioId)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(usuarioId);

        if (usuario is null)
        {
            throw new NotFoundException("Usuario no encontrado.");
        }

        return new ExportAccountDto
        {
            Id = usuario.Id,
            Nombre = usuario.Nombre,
            Correo = usuario.Correo,
            Telefono = usuario.Telefono,
            PlanActivo = usuario.PlanActivo,
            CreatedAt = usuario.CreatedAt,
            LastLoginAt = usuario.LastLoginAt,
            EmailConfirmed = usuario.EmailConfirmed
        };
    }

    private async Task<string> CreateRefreshTokenAsync(Usuario usuario)
    {
        var token = _tokenService.GenerateRefreshToken();
        var refreshToken = new RefreshToken
        {
            UsuarioId = usuario.Id,
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        await _refreshTokenRepository.AddAsync(refreshToken);
        return token;
    }

    private static AuthResponse CreateAuthResponse(Usuario usuario, string accessToken, string refreshToken, string mensaje)
    {
        return new AuthResponse
        {
            Success = true,
            Token = accessToken,
            RefreshToken = refreshToken,
            Mensaje = mensaje,
            Usuario = new UsuarioDto
            {
                Id = usuario.Id,
                Username = usuario.Username,
                AppId = usuario.AppId,
                Nombre = usuario.Nombre,
                Correo = usuario.Correo,
                Telefono = usuario.Telefono,
                PlanActivo = usuario.PlanActivo
            }
        };
    }

    private static string GenerateUsername(string nombre)
    {
        var baseName = "@" + nombre.ToLowerInvariant()
            .Replace(" ", "_")
            .Replace(".", "_")
            .Replace("-", "_");
        var suffix = Random.Shared.Next(100, 999);
        return $"{baseName}_{suffix}";
    }

    private static string GenerateAppId(string nombre)
    {
        var parts = nombre.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var initials = string.Concat(parts.Select(p => char.ToUpperInvariant(p[0])));
        if (initials.Length > 4) initials = initials[..4];
        return $"IX-{initials}-{DateTime.UtcNow.Year}";
    }

    private static string GenerateInviteCode(string nombre)
    {
        var parts = nombre.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var prefix = parts.Length > 0
            ? parts[0][..Math.Min(3, parts[0].Length)].ToUpperInvariant()
            : "USR";
        var digits = Random.Shared.Next(1000, 9999);
        return $"FAM-{prefix}-{digits}";
    }
}
