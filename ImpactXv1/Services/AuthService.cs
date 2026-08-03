using ImpactX.Core.Domain;
using ImpactX.Core.Exceptions;
using ImpactX.Core.Identity;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Core.Interfaces.Services;
using ImpactX.Core.Security;
using ImpactX.Models.DTOs;
using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ImpactX.Services;

public class AuthService : IAuthService
{
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordResetTokenRepository _passwordResetTokenRepository;
    private readonly IDispositivoRepository _dispositivoRepository;
    private readonly IEncryptionService _encryptionService;
    private readonly ITokenService _tokenService;
    private readonly IEmailService _emailService;
    private readonly IPlanRepository _planRepository;
    private readonly ISuscripcionRepository _suscripcionRepository;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUsuarioRepository usuarioRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordResetTokenRepository passwordResetTokenRepository,
        IDispositivoRepository dispositivoRepository,
        IEncryptionService encryptionService,
        ITokenService tokenService,
        IEmailService emailService,
        IPlanRepository planRepository,
        ISuscripcionRepository suscripcionRepository,
        ILogger<AuthService> logger)
    {
        _usuarioRepository = usuarioRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordResetTokenRepository = passwordResetTokenRepository;
        _dispositivoRepository = dispositivoRepository;
        _encryptionService = encryptionService;
        _tokenService = tokenService;
        _emailService = emailService;
        _planRepository = planRepository;
        _suscripcionRepository = suscripcionRepository;
        _logger = logger;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        ValidateRegistrationRequest(request);

        var correoNormalizado = EmailNormalizer.Normalize(request.Correo);
        if (await _usuarioRepository.ExistsByCorreoAsync(request.Correo))
        {
            return new AuthResponse
            {
                Success = false,
                Mensaje = "El correo ya está registrado."
            };
        }

        var username = await ResolveRegistrationUsernameAsync(request);
        var publicProfileId = await GenerateUniquePublicProfileIdAsync();
        var now = DateTime.UtcNow;

        var usuario = new Usuario
        {
            Nombre = NormalizeDisplayName(request.Nombre),
            Username = username,
            PublicProfileId = publicProfileId,
            CorreoNormalizado = correoNormalizado,
            AppId = GenerateAppId(request.Nombre),
            InviteCode = GenerateInviteCode(request.Nombre),
            Correo = request.Correo.Trim(),
            Telefono = request.Telefono?.Trim() ?? string.Empty,
            PasswordHash = _encryptionService.HashPassword(request.Password),
            PlanActivo = PlanNamePolicy.Free,
            CreatedAt = now,
            Onboarding = CreateInitialOnboarding(request, now)
        };

        await _usuarioRepository.AddAsync(usuario);

        try
        {
            var freePlan = await _planRepository.GetByNameAsync("Free");
            if (freePlan is not null)
            {
                var suscripcion = new Suscripcion
                {
                    UsuarioId = usuario.Id,
                    PlanId = freePlan.Id,
                    Estado = "Activa",
                    Inicio = DateTime.UtcNow,
                    Fin = null,
                    TrialFin = null,
                    AutoRenew = false,
                    BillingCycle = "Free",
                    UpdatedAtUtc = DateTime.UtcNow
                };
                await _suscripcionRepository.AddAsync(suscripcion);
                usuario.PlanActivo = "Free";
                await _usuarioRepository.UpdateAsync(usuario);
            }
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict
            || ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            _logger.LogWarning("Asignación del plan Free omitida (Cosmos {StatusCode}).",
                ex.StatusCode);
        }
        catch (DbUpdateException)
        {
            _logger.LogWarning("Asignación del plan Free omitida (EF).");
        }
        catch (OperationCanceledException)
        {
            throw;
        }

        var client = ClientTypePolicy.Normalize(request.Client);
        var accessToken = GenerateAccessToken(usuario, client);
        var refreshToken = await CreateRefreshTokenAsync(usuario, client);

        return CreateAuthResponse(usuario, accessToken, refreshToken, "Registro exitoso.");
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Identifier) &&
            !string.IsNullOrWhiteSpace(request.Correo) &&
            !string.Equals(request.Identifier.Trim(), request.Correo.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new BadRequestException("No se pueden proporcionar 'identifier' y 'correo' simultáneamente con valores diferentes.");
        }

        var identifier = ResolveIdentifier(request);
        var usuario = LooksLikeEmail(identifier)
            ? await _usuarioRepository.GetByCorreoAsync(identifier)
            : await _usuarioRepository.GetByUsernameAsync(identifier);

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

        await EnsureIdentityCompatibilityAsync(usuario);
        usuario.LastLoginAt = DateTime.UtcNow;
        await _usuarioRepository.UpdateAsync(usuario);

        var client = ClientTypePolicy.Normalize(request.Client);
        var accessToken = GenerateAccessToken(usuario, client);
        var refreshToken = await CreateRefreshTokenAsync(usuario, client);

        return CreateAuthResponse(usuario, accessToken, refreshToken, "Inicio de sesión exitoso.");
    }

    private static string ResolveIdentifier(LoginRequest request)
    {
        return string.IsNullOrWhiteSpace(request.Identifier)
            ? (request.Correo?.Trim() ?? string.Empty)
            : request.Identifier.Trim();
    }

    private static bool LooksLikeEmail(string identifier)
    {
        return identifier.IndexOf('@') > 0;
    }

    private async Task<bool> EnsureIdentityCompatibilityAsync(Usuario usuario)
    {
        var changed = false;

        if (string.IsNullOrWhiteSpace(usuario.PublicProfileId))
        {
            usuario.PublicProfileId = await GenerateUniquePublicProfileIdAsync();
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(usuario.CorreoNormalizado))
        {
            usuario.CorreoNormalizado = EmailNormalizer.Normalize(usuario.Correo);
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(usuario.Username))
        {
            usuario.Username = await GenerateUniqueUsernameAsync(usuario.Nombre);
            changed = true;
            _logger.LogWarning("Cuenta legacy sin username; se generó uno automáticamente.");
        }
        else if (usuario.Username.StartsWith("@", StringComparison.Ordinal))
        {
            var normalized = UsernamePolicy.Normalize(usuario.Username.TrimStart('@'));
            if (normalized is not null && normalized != usuario.Username
                && !await _usuarioRepository.ExistsByUsernameAsync(normalized))
            {
                usuario.Username = normalized;
                changed = true;
                _logger.LogWarning("Username legacy con '@' migrado a formato estándar.");
            }
        }

        if (usuario.Onboarding is null)
        {
            usuario.Onboarding = new OnboardingProgress();
            changed = true;
        }
        else if (usuario.Onboarding.RegistrationContractVersion < RegistrationContract.LegacyVersion)
        {
            usuario.Onboarding.RegistrationContractVersion = RegistrationContract.LegacyVersion;
            changed = true;
        }

        return changed;
    }

    public async Task<RecoverPasswordResponse> RecoverPasswordAsync(RecoverPasswordRequest request)
    {
        var usuario = await _usuarioRepository.GetByCorreoAsync(request.Correo);

        if (usuario is not null)
        {
            var token = _tokenService.GeneratePasswordResetToken();
            var now = DateTime.UtcNow;
            var resetToken = new PasswordResetToken
            {
                UsuarioId = usuario.Id,
                TokenHash = PasswordResetTokenHasher.Hash(token),
                CreatedAt = now,
                ExpiresAt = now.AddHours(1)
            };

            await _passwordResetTokenRepository.InvalidateAllByUsuarioIdAsync(usuario.Id, now);
            await _passwordResetTokenRepository.AddAsync(resetToken);
            await _emailService.SendPasswordResetEmailAsync(usuario.Correo, token);
        }

        return new RecoverPasswordResponse
        {
            Success = true,
            Mensaje = "Si la cuenta existe, se enviaron instrucciones para restablecer la contraseña."
        };
    }

    public async Task<AuthResponse> ResetPasswordAsync(ResetPasswordRequest request)
    {
        var resetToken = await _passwordResetTokenRepository.GetByTokenHashAsync(
            PasswordResetTokenHasher.Hash(request.Token));

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

        await _refreshTokenRepository.RevokeAllByUsuarioIdAsync(usuario.Id, DateTime.UtcNow);

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

        await _refreshTokenRepository.RevokeAllByUsuarioIdAsync(usuario.Id, DateTime.UtcNow);

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
            usuario.FcmToken = null;
            await _usuarioRepository.UpdateAsync(usuario);
            await _refreshTokenRepository.RevokeAllByUsuarioIdAsync(usuarioId, DateTime.UtcNow);
            await _dispositivoRepository.DeleteAllByUsuarioIdAsync(usuarioId);
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
            PublicProfileId = string.IsNullOrWhiteSpace(usuario.PublicProfileId)
                ? usuario.Username
                : usuario.PublicProfileId,
            Nombre = usuario.Nombre,
            Correo = usuario.Correo,
            Telefono = usuario.Telefono,
            PlanActivo = usuario.PlanActivo,
            CreatedAt = usuario.CreatedAt,
            LastLoginAt = usuario.LastLoginAt,
            EmailConfirmed = usuario.EmailConfirmed,
            Onboarding = OnboardingDtoMapper.Map(usuario.Onboarding)
        };
    }

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, string? ipAddress = null, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return new AuthResponse
            {
                Success = false,
                Mensaje = "Autenticación fallida."
            };
        }

        var existingToken = await _refreshTokenRepository.GetByTokenAsync(request.RefreshToken);

        if (existingToken is null || existingToken.IsExpired || existingToken.RevokedAt is not null)
        {
            return new AuthResponse
            {
                Success = false,
                Mensaje = "Autenticación fallida."
            };
        }

        var usuario = await _usuarioRepository.GetByIdAsync(existingToken.UsuarioId);

        if (usuario is null || !usuario.IsActive)
        {
            return new AuthResponse
            {
                Success = false,
                Mensaje = "Autenticación fallida."
            };
        }

        // Rotación: revocar token anterior y crear uno nuevo
        existingToken.RevokedAt = DateTime.UtcNow;
        await _refreshTokenRepository.UpdateAsync(existingToken);

        if (await EnsureIdentityCompatibilityAsync(usuario))
        {
            await _usuarioRepository.UpdateAsync(usuario);
        }

        var client = ClientTypePolicy.Normalize(existingToken.Client);
        var accessToken = GenerateAccessToken(usuario, client);
        var newRefreshToken = await CreateRefreshTokenAsync(usuario, client);

        return CreateAuthResponse(usuario, accessToken, newRefreshToken, "Sesión renovada exitosamente.");
    }

    private async Task<string> ResolveRegistrationUsernameAsync(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
        {
            return await GenerateUniqueUsernameAsync(request.Nombre);
        }

        var username = UsernamePolicy.Normalize(request.Username);
        if (username is null)
        {
            throw new BadRequestException(
                "El username debe tener entre 3 y 30 caracteres y usar solo letras, números, punto o guion bajo.");
        }

        if (UsernamePolicy.IsReserved(username))
        {
            throw new ConflictException("Ese username no está disponible.");
        }

        if (await _usuarioRepository.ExistsByUsernameIncludingHistoryAsync(username))
        {
            throw new ConflictException("Ese username ya está en uso.");
        }

        return username;
    }

    private static void ValidateRegistrationRequest(RegisterRequest request)
    {
        if (request.RegistrationVersion == RegistrationContract.LegacyVersion)
        {
            return;
        }

        if (request.RegistrationVersion != RegistrationContract.CurrentVersion)
        {
            throw new BadRequestException("Versión de contrato de registro no compatible.");
        }

        var client = ClientTypePolicy.Normalize(request.Client);
        if (!RegistrationContract.SupportedAccountClients.Contains(client, StringComparer.Ordinal))
        {
            throw new BadRequestException("La creación de cuentas solo está disponible para web y mobile.");
        }

        if (string.IsNullOrWhiteSpace(request.Username))
        {
            throw new BadRequestException("El username es obligatorio para el registro completo.");
        }

        if (!RegistrationContract.IsValidPhone(request.Telefono))
        {
            throw new BadRequestException("El teléfono es obligatorio y debe contener entre 7 y 15 dígitos válidos.");
        }

        if (!RegistrationContract.IsStrongPassword(request.Password))
        {
            throw new BadRequestException(
                "La contraseña debe incluir mayúscula, minúscula, número y carácter especial.");
        }

        if (request.TermsAccepted is not true || request.PrivacyAccepted is not true)
        {
            throw new BadRequestException(
                "Debes aceptar los términos de uso y el aviso de privacidad para crear la cuenta.");
        }
    }

    private static OnboardingProgress CreateInitialOnboarding(RegisterRequest request, DateTime now)
    {
        var completeContract = request.RegistrationVersion == RegistrationContract.CurrentVersion;
        var termsAccepted = request.TermsAccepted is true;
        var privacyAccepted = request.PrivacyAccepted is true;

        return new OnboardingProgress
        {
            RegistrationContractVersion = completeContract
                ? RegistrationContract.CurrentVersion
                : RegistrationContract.LegacyVersion,
            CurrentStep = completeContract ? 3 : OnboardingProgress.MinCurrentStep,
            TermsAccepted = termsAccepted,
            TermsVersion = termsAccepted ? RegistrationContract.TermsVersion : null,
            TermsAcceptedAtUtc = termsAccepted ? now : null,
            PrivacyAccepted = privacyAccepted,
            PrivacyNoticeVersion = privacyAccepted ? RegistrationContract.PrivacyNoticeVersion : null,
            PrivacyAcceptedAtUtc = privacyAccepted ? now : null,
            LocationIncidentConsent = request.LocationIncidentConsent is true,
            DrivingPatternConsent = request.DrivingPatternConsent is true,
            UpdatedAtUtc = now
        };
    }

    private static string NormalizeDisplayName(string nombre)
    {
        return string.Join(' ', nombre.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private string GenerateAccessToken(Usuario usuario, string client)
    {
        return _tokenService is IClientAwareTokenService clientAwareTokenService
            ? clientAwareTokenService.GenerateAccessToken(usuario, client)
            : _tokenService.GenerateAccessToken(usuario);
    }

    private async Task<string> CreateRefreshTokenAsync(Usuario usuario, string client)
    {
        var token = _tokenService.GenerateRefreshToken();
        var refreshToken = new RefreshToken
        {
            UsuarioId = usuario.Id,
            Token = token,
            Client = ClientTypePolicy.Normalize(client),
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
                Id = usuario.PublicProfileId,
                PublicProfileId = usuario.PublicProfileId,
                Username = usuario.Username,
                Nombre = usuario.Nombre,
                Correo = usuario.Correo,
                Telefono = usuario.Telefono,
                PlanActivo = usuario.PlanActivo,
                Onboarding = OnboardingDtoMapper.Map(usuario.Onboarding)
            }
        };
    }

    private async Task<string> GenerateUniqueUsernameAsync(string nombre)
    {
        for (var i = 0; i < 25; i++)
        {
            var candidate = UsernamePolicy.Generate(nombre);
            if (!await _usuarioRepository.ExistsByUsernameIncludingHistoryAsync(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("No se pudo generar un username único.");
    }

    private async Task<string> GenerateUniquePublicProfileIdAsync()
    {
        for (var i = 0; i < 25; i++)
        {
            var candidate = PublicProfileIdGenerator.Generate();
            if (!await _usuarioRepository.ExistsByPublicProfileIdAsync(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("No se pudo generar un PublicProfileId único.");
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
