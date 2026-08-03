using ImpactX.Core.Domain;
using ImpactX.Core.Exceptions;
using ImpactX.Core.Identity;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Models.DTOs;

namespace ImpactX.Services;

public class UserService : IUserService
{
    private readonly IUsuarioRepository _usuarioRepository;

    public UserService(IUsuarioRepository usuarioRepository)
    {
        _usuarioRepository = usuarioRepository;
    }

    public async Task<UserProfileDto> GetProfileAsync(Guid usuarioId)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(usuarioId);
        if (usuario is null)
            throw new NotFoundException("Usuario no encontrado.");

        if (await EnsureIdentityCompatibilityAsync(usuario))
            await _usuarioRepository.UpdateAsync(usuario);

        return MapToProfileDto(usuario);
    }

    public async Task<UserProfileDto> UpdateProfileAsync(Guid usuarioId, UpdateUserProfileRequest request)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(usuarioId);
        if (usuario is null)
            throw new NotFoundException("Usuario no encontrado.");

        await EnsureIdentityCompatibilityAsync(usuario);

        if (request.Nombre is not null)
            usuario.Nombre = request.Nombre;
        if (request.Telefono is not null)
            usuario.Telefono = request.Telefono;

        await _usuarioRepository.UpdateAsync(usuario);
        return MapToProfileDto(usuario);
    }

    public async Task<UserPreferencesDto> GetPreferencesAsync(Guid usuarioId)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(usuarioId);
        if (usuario is null)
            throw new NotFoundException("Usuario no encontrado.");

        return MapToPreferencesDto(usuario.Preferencias) ?? new UserPreferencesDto();
    }

    public async Task<UserPreferencesDto> UpdatePreferencesAsync(Guid usuarioId, UpdateUserPreferencesRequest request)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(usuarioId);
        if (usuario is null)
            throw new NotFoundException("Usuario no encontrado.");

        usuario.Preferencias ??= new PreferenciasUsuario();

        if (request.NotificacionesPush.HasValue)
            usuario.Preferencias.NotificacionesPush = request.NotificacionesPush.Value;
        if (request.NotificacionesEmail.HasValue)
            usuario.Preferencias.NotificacionesEmail = request.NotificacionesEmail.Value;
        if (request.CompartirUbicacion.HasValue)
            usuario.Preferencias.CompartirUbicacion = request.CompartirUbicacion.Value;
        if (request.Idioma is not null)
            usuario.Preferencias.Idioma = request.Idioma;
        if (request.UnidadVelocidad is not null)
            usuario.Preferencias.UnidadVelocidad = request.UnidadVelocidad;

        await _usuarioRepository.UpdateAsync(usuario);
        return MapToPreferencesDto(usuario.Preferencias) ?? new UserPreferencesDto();
    }

    public async Task<DriverProfileDto> GetDriverProfileAsync(Guid usuarioId)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(usuarioId);
        if (usuario is null)
            throw new NotFoundException("Usuario no encontrado.");

        return MapToDriverProfileDto(usuario.PerfilConduccion) ?? new DriverProfileDto();
    }

    public async Task<DriverProfileDto> UpdateDriverProfileAsync(Guid usuarioId, UpdateDriverProfileRequest request)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(usuarioId);
        if (usuario is null)
            throw new NotFoundException("Usuario no encontrado.");

        usuario.PerfilConduccion ??= new PerfilConduccion();

        if (request.TipoVehiculo is not null)
            usuario.PerfilConduccion.TipoVehiculo = request.TipoVehiculo;
        if (request.Marca is not null)
            usuario.PerfilConduccion.Marca = request.Marca;
        if (request.Modelo is not null)
            usuario.PerfilConduccion.Modelo = request.Modelo;
        if (request.Anio.HasValue)
            usuario.PerfilConduccion.Anio = request.Anio;
        if (request.Color is not null)
            usuario.PerfilConduccion.Color = request.Color;
        if (request.Placa is not null)
            usuario.PerfilConduccion.Placa = request.Placa;
        if (request.Uso is not null)
            usuario.PerfilConduccion.Uso = request.Uso;
        if (request.VelocidadPromedioLabel is not null)
            usuario.PerfilConduccion.VelocidadPromedioLabel = request.VelocidadPromedioLabel;

        await _usuarioRepository.UpdateAsync(usuario);
        return MapToDriverProfileDto(usuario.PerfilConduccion) ?? new DriverProfileDto();
    }

    public async Task<MedicalProfileDto> GetMedicalProfileAsync(Guid usuarioId)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(usuarioId);
        if (usuario is null)
            throw new NotFoundException("Usuario no encontrado.");

        return MapToMedicalProfileDto(usuario.FichaMedica) ?? new MedicalProfileDto();
    }

    public async Task<MedicalProfileDto> UpdateMedicalProfileAsync(Guid usuarioId, UpdateMedicalProfileRequest request)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(usuarioId);
        if (usuario is null)
            throw new NotFoundException("Usuario no encontrado.");

        usuario.FichaMedica ??= new FichaMedica();

        if (request.TipoSangre is not null)
            usuario.FichaMedica.TipoSangre = request.TipoSangre;
        if (request.Alergias is not null)
            usuario.FichaMedica.Alergias = request.Alergias;
        if (request.Condiciones is not null)
            usuario.FichaMedica.Condiciones = request.Condiciones;
        if (request.Medicamentos is not null)
            usuario.FichaMedica.Medicamentos = request.Medicamentos;
        if (request.Nota is not null)
            usuario.FichaMedica.Nota = request.Nota;

        usuario.Onboarding ??= new OnboardingProgress();
        usuario.Onboarding.MedicalProfileStatus = MedicalProfileOnboardingStatus.Completed;
        RecomputeOnboardingCompletion(usuario.Onboarding, DateTime.UtcNow);
        usuario.Onboarding.UpdatedAtUtc = DateTime.UtcNow;

        await _usuarioRepository.UpdateAsync(usuario);
        return MapToMedicalProfileDto(usuario.FichaMedica) ?? new MedicalProfileDto();
    }

    public async Task<OnboardingDto> GetOnboardingAsync(Guid usuarioId)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(usuarioId);
        if (usuario is null)
            throw new NotFoundException("Usuario no encontrado.");

        usuario.Onboarding ??= new OnboardingProgress();
        return MapToOnboardingDto(usuario.Onboarding) ?? new OnboardingDto();
    }

    public async Task<OnboardingDto> UpdateOnboardingAsync(Guid usuarioId, UpdateOnboardingRequest request)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(usuarioId);
        if (usuario is null)
            throw new NotFoundException("Usuario no encontrado.");

        await EnsureIdentityCompatibilityAsync(usuario);
        usuario.Onboarding ??= new OnboardingProgress();
        var onboarding = usuario.Onboarding;

        if (request.CurrentStep.HasValue)
        {
            if (request.CurrentStep.Value is < 1 or > 8)
                throw new BadRequestException("El paso de onboarding debe estar entre 1 y 8.");
            if (request.CurrentStep.Value > onboarding.CurrentStep)
                onboarding.CurrentStep = request.CurrentStep.Value;
        }

        if (request.PrivacyAccepted.HasValue)
        {
            if (!request.PrivacyAccepted.Value && onboarding.PrivacyAccepted)
            {
                throw new BadRequestException(
                    "La aceptación del aviso de privacidad no se revoca desde el onboarding. Administra por separado los consentimientos opcionales.");
            }

            if (request.PrivacyAccepted.Value && !onboarding.PrivacyAccepted)
            {
                onboarding.PrivacyAccepted = true;
                onboarding.PrivacyNoticeVersion = RegistrationContract.PrivacyNoticeVersion;
                onboarding.PrivacyAcceptedAtUtc = DateTime.UtcNow;
            }
        }
        if (request.LocationIncidentConsent.HasValue)
            onboarding.LocationIncidentConsent = request.LocationIncidentConsent.Value;
        if (request.DrivingPatternConsent.HasValue)
            onboarding.DrivingPatternConsent = request.DrivingPatternConsent.Value;

        if (!string.IsNullOrWhiteSpace(request.MedicalProfileStatus))
        {
            if (!Enum.TryParse<MedicalProfileOnboardingStatus>(request.MedicalProfileStatus, true, out var medicalStatus))
                throw new BadRequestException("Estado de perfil médico inválido.");
            onboarding.MedicalProfileStatus = medicalStatus;
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            onboarding.Status = request.Status switch
            {
                "Completed" => OnboardingStatus.Completed,
                "Pending" => OnboardingStatus.Pending,
                _ => throw new BadRequestException("Estado de onboarding inválido.")
            };
        }

        RecomputeOnboardingCompletion(onboarding, DateTime.UtcNow);
        onboarding.UpdatedAtUtc = DateTime.UtcNow;
        await _usuarioRepository.UpdateAsync(usuario);

        return MapToOnboardingDto(onboarding) ?? new OnboardingDto();
    }

    public async Task<OnboardingDto> AcceptLegalDocumentsAsync(
        Guid usuarioId,
        AcceptLegalDocumentsRequest request)
    {
        if (request.ContractVersion != RegistrationContract.CurrentVersion)
        {
            throw new BadRequestException("Versión de contrato legal no compatible.");
        }

        if (!request.TermsAccepted || !request.PrivacyAccepted)
        {
            throw new BadRequestException(
                "Debes aceptar los términos de uso y el aviso de privacidad vigentes.");
        }

        var usuario = await _usuarioRepository.GetByIdAsync(usuarioId);
        if (usuario is null)
        {
            throw new NotFoundException("Usuario no encontrado.");
        }

        await EnsureIdentityCompatibilityAsync(usuario);
        usuario.Onboarding ??= new OnboardingProgress();
        var onboarding = usuario.Onboarding;
        var now = DateTime.UtcNow;

        var termsVersionChanged = !string.Equals(
            onboarding.TermsVersion,
            RegistrationContract.TermsVersion,
            StringComparison.Ordinal);
        var privacyVersionChanged = !string.Equals(
            onboarding.PrivacyNoticeVersion,
            RegistrationContract.PrivacyNoticeVersion,
            StringComparison.Ordinal);

        onboarding.RegistrationContractVersion = RegistrationContract.CurrentVersion;
        onboarding.TermsAccepted = true;
        onboarding.TermsVersion = RegistrationContract.TermsVersion;
        if (termsVersionChanged || !onboarding.TermsAcceptedAtUtc.HasValue)
        {
            onboarding.TermsAcceptedAtUtc = now;
        }

        onboarding.PrivacyAccepted = true;
        onboarding.PrivacyNoticeVersion = RegistrationContract.PrivacyNoticeVersion;
        if (privacyVersionChanged || !onboarding.PrivacyAcceptedAtUtc.HasValue)
        {
            onboarding.PrivacyAcceptedAtUtc = now;
        }
        onboarding.UpdatedAtUtc = now;

        RecomputeOnboardingCompletion(onboarding, now);
        await _usuarioRepository.UpdateAsync(usuario);

        return OnboardingDtoMapper.Map(onboarding) ?? new OnboardingDto();
    }

    public async Task<UserProfileDto> UpdateUsernameAsync(Guid usuarioId, UpdateUsernameRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
            throw new BadRequestException("El username es obligatorio.");

        var username = UsernamePolicy.Normalize(request.Username);
        if (username is null || username.Length is < 3 or > 30)
            throw new BadRequestException("El username debe tener entre 3 y 30 caracteres (a-z, 0-9, punto y guion bajo).");

        if (UsernamePolicy.IsReserved(username))
            throw new ConflictException("Ese username está reservado.");

        var usuario = await _usuarioRepository.GetByIdAsync(usuarioId);
        if (usuario is null)
            throw new NotFoundException("Usuario no encontrado.");

        if (string.Equals(usuario.Username, username, StringComparison.OrdinalIgnoreCase))
            return MapToProfileDto(usuario);

        if (await _usuarioRepository.ExistsByUsernameAsync(username)
            || await _usuarioRepository.ExistsByUsernameHistoryExcludingUsuarioAsync(username, usuarioId))
            throw new ConflictException("Ese username ya está en uso.");

        if (!string.IsNullOrWhiteSpace(usuario.Username))
        {
            usuario.UsernamesAnteriores ??= new List<string>();
            if (!usuario.UsernamesAnteriores.Contains(usuario.Username, StringComparer.OrdinalIgnoreCase))
            {
                usuario.UsernamesAnteriores.Add(usuario.Username);
            }
        }

        usuario.Username = username;
        await _usuarioRepository.UpdateAsync(usuario);
        return MapToProfileDto(usuario);
    }

    public async Task UpdateFcmTokenAsync(Guid usuarioId, UpdateFcmTokenRequest request)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(usuarioId);
        if (usuario is null)
            throw new NotFoundException("Usuario no encontrado.");

        usuario.FcmToken = request.Token;
        await _usuarioRepository.UpdateAsync(usuario);
    }

    public async Task DeleteFcmTokenAsync(Guid usuarioId)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(usuarioId);
        if (usuario is null)
            throw new NotFoundException("Usuario no encontrado.");

        usuario.FcmToken = null;
        await _usuarioRepository.UpdateAsync(usuario);
    }

    public async Task<List<UserSearchResultDto>> SearchUsersAsync(string query, string? by = null, Guid? excludeUserId = null)
    {
        var users = await _usuarioRepository.SearchAsync(query, by);

        return users
            .Where(u => excludeUserId is null || u.Id != excludeUserId.Value)
            .Select(u => new UserSearchResultDto
            {
                Id = u.PublicProfileId,
                PublicProfileId = u.PublicProfileId,
                Username = u.Username,
                Nombre = u.Nombre
            })
            .ToList();
    }

    private static void RecomputeOnboardingCompletion(OnboardingProgress o, DateTime now)
    {
        var legalOk = o.RegistrationContractVersion < RegistrationContract.CurrentVersion
            ? o.PrivacyAccepted
            : o.TermsAccepted && o.PrivacyAccepted;
        var medicalOk = o.MedicalProfileStatus is MedicalProfileOnboardingStatus.Completed or MedicalProfileOnboardingStatus.Skipped;
        var completionCriteriaMet = o.CurrentStep >= 8 && legalOk && medicalOk;

        if (completionCriteriaMet && o.Status != OnboardingStatus.Completed)
        {
            o.Status = OnboardingStatus.Completed;
            o.CompletedAtUtc = now;
        }
        else if (!completionCriteriaMet && o.Status == OnboardingStatus.Completed)
        {
            o.Status = OnboardingStatus.Pending;
            o.CompletedAtUtc = null;
        }
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

    private async Task<string> GenerateUniquePublicProfileIdAsync()
    {
        for (var i = 0; i < 25; i++)
        {
            var candidate = PublicProfileIdGenerator.Generate();
            if (!await _usuarioRepository.ExistsByPublicProfileIdAsync(candidate))
                return candidate;
        }

        throw new InvalidOperationException("No se pudo generar un PublicProfileId único.");
    }

    private static UserProfileDto MapToProfileDto(Usuario u)
    {
        return new UserProfileDto
        {
            Id = u.PublicProfileId,
            PublicProfileId = u.PublicProfileId,
            Username = u.Username,
            Nombre = u.Nombre,
            Correo = u.Correo,
            Telefono = u.Telefono,
            PlanActivo = u.PlanActivo,
            EmailConfirmed = u.EmailConfirmed,
            CreatedAt = u.CreatedAt,
            LastLoginAt = u.LastLoginAt,
            Onboarding = MapToOnboardingDto(u.Onboarding),
            PerfilConduccion = MapToDriverProfileDto(u.PerfilConduccion),
            FichaMedica = MapToMedicalProfileDto(u.FichaMedica),
            Preferencias = MapToPreferencesDto(u.Preferencias),
            Permisos = u.Permisos is not null ? new PermisosDto
            {
                Mobile = u.Permisos.Mobile is not null ? new PermisosPlataformaDto
                {
                    Ubicacion = u.Permisos.Mobile.Ubicacion,
                    Notificaciones = u.Permisos.Mobile.Notificaciones,
                    Camara = u.Permisos.Mobile.Camara,
                    Microfono = u.Permisos.Mobile.Microfono,
                    Sensores = u.Permisos.Mobile.Sensores,
                    Bluetooth = u.Permisos.Mobile.Bluetooth,
                } : null,
                Web = u.Permisos.Web is not null ? new PermisosPlataformaDto
                {
                    Ubicacion = u.Permisos.Web.Ubicacion,
                    Notificaciones = u.Permisos.Web.Notificaciones,
                    Camara = u.Permisos.Web.Camara,
                    Microfono = u.Permisos.Web.Microfono,
                    Sensores = u.Permisos.Web.Sensores,
                    Bluetooth = u.Permisos.Web.Bluetooth,
                } : null,
            } : null,
            Settings = u.Settings is not null ? new SettingsDto
            {
                TwoFactorEnabled = u.Settings.TwoFactorEnabled
            } : null,
        };
    }

    private static OnboardingDto? MapToOnboardingDto(OnboardingProgress? onboarding)
    {
        return OnboardingDtoMapper.Map(onboarding);
    }

    private static DriverProfileDto? MapToDriverProfileDto(PerfilConduccion? p)
    {
        return p is null ? null : new DriverProfileDto
        {
            TipoVehiculo = p.TipoVehiculo,
            Marca = p.Marca,
            Modelo = p.Modelo,
            Anio = p.Anio,
            Color = p.Color,
            Placa = p.Placa,
            Uso = p.Uso,
            VelocidadPromedioLabel = p.VelocidadPromedioLabel,
        };
    }

    private static MedicalProfileDto? MapToMedicalProfileDto(FichaMedica? m)
    {
        return m is null ? null : new MedicalProfileDto
        {
            TipoSangre = m.TipoSangre,
            Alergias = m.Alergias,
            Condiciones = m.Condiciones,
            Medicamentos = m.Medicamentos,
            Nota = m.Nota,
        };
    }

    private static UserPreferencesDto? MapToPreferencesDto(PreferenciasUsuario? p)
    {
        return p is null ? null : new UserPreferencesDto
        {
            NotificacionesPush = p.NotificacionesPush,
            NotificacionesEmail = p.NotificacionesEmail,
            CompartirUbicacion = p.CompartirUbicacion,
            Idioma = p.Idioma,
            UnidadVelocidad = p.UnidadVelocidad,
        };
    }
}
