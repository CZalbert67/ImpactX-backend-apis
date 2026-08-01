using ImpactX.Core.Domain;
using ImpactX.Core.Exceptions;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Models.DTOs;

namespace ImpactX.Services;

public class UserService : IUserService
{
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly ISuscripcionRepository _suscripcionRepository;
    private readonly IPlanRepository _planRepository;

    public UserService(
        IUsuarioRepository usuarioRepository,
        ISuscripcionRepository suscripcionRepository,
        IPlanRepository planRepository)
    {
        _usuarioRepository = usuarioRepository;
        _suscripcionRepository = suscripcionRepository;
        _planRepository = planRepository;
    }

    public async Task<UserProfileDto> GetProfileAsync(Guid usuarioId)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(usuarioId);
        if (usuario is null)
            throw new NotFoundException("Usuario no encontrado.");

        return await MapToProfileDtoAsync(usuario);
    }

    public async Task<UserProfileDto> UpdateProfileAsync(Guid usuarioId, UpdateUserProfileRequest request)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(usuarioId);
        if (usuario is null)
            throw new NotFoundException("Usuario no encontrado.");

        if (request.Nombre is not null)
            usuario.Nombre = request.Nombre;
        if (request.Telefono is not null)
            usuario.Telefono = request.Telefono;
        if (request.Ciudad is not null)
            usuario.Ciudad = request.Ciudad;

        await _usuarioRepository.UpdateAsync(usuario);
        return await MapToProfileDtoAsync(usuario);
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
        if (request.NotificacionesSms.HasValue)
            usuario.Preferencias.NotificacionesSms = request.NotificacionesSms.Value;
        if (request.NotificacionesWhatsapp.HasValue)
            usuario.Preferencias.NotificacionesWhatsapp = request.NotificacionesWhatsapp.Value;
        if (request.CompartirUbicacion.HasValue)
            usuario.Preferencias.CompartirUbicacion = request.CompartirUbicacion.Value;
        if (request.Idioma is not null)
            usuario.Preferencias.Idioma = request.Idioma;
        if (request.UnidadVelocidad is not null)
            usuario.Preferencias.UnidadVelocidad = request.UnidadVelocidad;
        if (request.ZonaHoraria is not null)
            usuario.Preferencias.ZonaHoraria = request.ZonaHoraria;

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
        if (request.TienePadecimiento is not null)
            usuario.FichaMedica.TienePadecimiento = request.TienePadecimiento;
        if (request.CompartirFichaMedica.HasValue)
            usuario.FichaMedica.CompartirFichaMedica = request.CompartirFichaMedica.Value;
        if (request.PermitirUbicacion.HasValue)
            usuario.FichaMedica.PermitirUbicacion = request.PermitirUbicacion.Value;
        if (request.PermitirAprendizajeIA.HasValue)
            usuario.FichaMedica.PermitirAprendizajeIA = request.PermitirAprendizajeIA.Value;

        await _usuarioRepository.UpdateAsync(usuario);
        return MapToMedicalProfileDto(usuario.FichaMedica) ?? new MedicalProfileDto();
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

    public async Task<List<UserSearchResultDto>> SearchUsersAsync(string query, Guid? excludeUserId = null)
    {
        var users = await _usuarioRepository.SearchAsync(query);

        return users
            .Where(u => excludeUserId is null || u.Id != excludeUserId.Value)
            .Select(u => new UserSearchResultDto
            {
                Id = u.Id,
                Username = u.Username,
                AppId = u.AppId,
                Nombre = u.Nombre
            })
            .ToList();
    }

    private async Task<UserProfileDto> MapToProfileDtoAsync(Usuario u)
    {
        string? plan = u.PlanActivo;
        string? subscriptionStatus = null;
        int? trialDaysLeft = null;
        DateTime? subscriptionStart = null;
        DateTime? subscriptionEnd = null;

        var suscripcion = await _suscripcionRepository.GetActiveByUserAsync(u.Id);
        if (suscripcion is not null)
        {
            var planEntity = await _planRepository.GetByIdAsync(suscripcion.PlanId);
            plan = suscripcion.Estado == "Trial"
                ? "trial"
                : planEntity?.Nombre.ToLowerInvariant() ?? "trial";
            subscriptionStatus = suscripcion.Estado;
            subscriptionStart = suscripcion.Inicio;
            subscriptionEnd = suscripcion.Fin ?? suscripcion.TrialFin;

            if (subscriptionEnd.HasValue)
                trialDaysLeft = Math.Max(0, (int)(subscriptionEnd.Value.Date - DateTime.UtcNow.Date).TotalDays);
        }

        return new UserProfileDto
        {
            Id = u.Id,
            Username = u.Username,
            AppId = u.AppId,
            InviteCode = u.InviteCode,
            Nombre = u.Nombre,
            Correo = u.Correo,
            Telefono = u.Telefono,
            Ciudad = u.Ciudad,
            Idioma = u.Preferencias?.Idioma,
            PlanActivo = u.PlanActivo,
            Plan = plan,
            SubscriptionStatus = subscriptionStatus,
            TrialDaysLeft = trialDaysLeft,
            SubscriptionStart = subscriptionStart,
            SubscriptionEnd = subscriptionEnd,
            OnboardingCompleto = u.OnboardingCompleto,
            TwoFactor = u.Settings?.TwoFactorEnabled ?? false,
            EmailConfirmed = u.EmailConfirmed,
            CreatedAt = u.CreatedAt,
            LastLoginAt = u.LastLoginAt,
            PerfilConduccion = MapToDriverProfileDto(u.PerfilConduccion),
            FichaMedica = MapToMedicalProfileDto(u.FichaMedica),
            Preferencias = MapToPreferencesDto(u.Preferencias),
            Permisos = u.Permisos is not null ? new PermisosDto
            {
                Mobile = MapToPermisosPlataformaDto(u.Permisos.Mobile),
                Web = MapToPermisosPlataformaDto(u.Permisos.Web),
            } : null,
            Settings = u.Settings is not null ? new SettingsDto
            {
                TwoFactorEnabled = u.Settings.TwoFactorEnabled
            } : null,
        };
    }

    private static PermisosPlataformaDto? MapToPermisosPlataformaDto(PermisosPlataforma? p)
    {
        return p is null ? null : new PermisosPlataformaDto
        {
            Ubicacion = p.Ubicacion,
            Notificaciones = p.Notificaciones,
            Camara = p.Camara,
            Microfono = p.Microfono,
            Sensores = p.Sensores,
            Bluetooth = p.Bluetooth,
            Llamadas = p.Llamadas,
            SegundoPlano = p.SegundoPlano,
            RitmoCardiaco = p.RitmoCardiaco,
        };
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
            TienePadecimiento = m.TienePadecimiento,
            CompartirFichaMedica = m.CompartirFichaMedica,
            PermitirUbicacion = m.PermitirUbicacion,
            PermitirAprendizajeIA = m.PermitirAprendizajeIA,
        };
    }

    private static UserPreferencesDto? MapToPreferencesDto(PreferenciasUsuario? p)
    {
        return p is null ? null : new UserPreferencesDto
        {
            NotificacionesPush = p.NotificacionesPush,
            NotificacionesEmail = p.NotificacionesEmail,
            NotificacionesSms = p.NotificacionesSms,
            NotificacionesWhatsapp = p.NotificacionesWhatsapp,
            CompartirUbicacion = p.CompartirUbicacion,
            Idioma = p.Idioma,
            UnidadVelocidad = p.UnidadVelocidad,
            ZonaHoraria = p.ZonaHoraria,
        };
    }
}
