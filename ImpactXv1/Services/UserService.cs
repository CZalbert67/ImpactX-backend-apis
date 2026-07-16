using Prueba1.Core.Domain;
using Prueba1.Core.Interfaces.Repositories;
using Prueba1.Models.DTOs;

namespace Prueba1.Services;

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
            throw new KeyNotFoundException("Usuario no encontrado.");

        return MapToProfileDto(usuario);
    }

    public async Task<UserProfileDto> UpdateProfileAsync(Guid usuarioId, UpdateUserProfileRequest request)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(usuarioId);
        if (usuario is null)
            throw new KeyNotFoundException("Usuario no encontrado.");

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
            throw new KeyNotFoundException("Usuario no encontrado.");

        return MapToPreferencesDto(usuario.Preferencias) ?? new UserPreferencesDto();
    }

    public async Task<UserPreferencesDto> UpdatePreferencesAsync(Guid usuarioId, UpdateUserPreferencesRequest request)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(usuarioId);
        if (usuario is null)
            throw new KeyNotFoundException("Usuario no encontrado.");

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
            throw new KeyNotFoundException("Usuario no encontrado.");

        return MapToDriverProfileDto(usuario.PerfilConduccion) ?? new DriverProfileDto();
    }

    public async Task<DriverProfileDto> UpdateDriverProfileAsync(Guid usuarioId, UpdateDriverProfileRequest request)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(usuarioId);
        if (usuario is null)
            throw new KeyNotFoundException("Usuario no encontrado.");

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
            throw new KeyNotFoundException("Usuario no encontrado.");

        return MapToMedicalProfileDto(usuario.FichaMedica) ?? new MedicalProfileDto();
    }

    public async Task<MedicalProfileDto> UpdateMedicalProfileAsync(Guid usuarioId, UpdateMedicalProfileRequest request)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(usuarioId);
        if (usuario is null)
            throw new KeyNotFoundException("Usuario no encontrado.");

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

        await _usuarioRepository.UpdateAsync(usuario);
        return MapToMedicalProfileDto(usuario.FichaMedica) ?? new MedicalProfileDto();
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
                Nombre = u.Nombre,
                Correo = u.Correo
            })
            .ToList();
    }

    private static UserProfileDto MapToProfileDto(Usuario u)
    {
        return new UserProfileDto
        {
            Id = u.Id,
            Username = u.Username,
            AppId = u.AppId,
            InviteCode = u.InviteCode,
            Nombre = u.Nombre,
            Correo = u.Correo,
            Telefono = u.Telefono,
            PlanActivo = u.PlanActivo,
            EmailConfirmed = u.EmailConfirmed,
            CreatedAt = u.CreatedAt,
            LastLoginAt = u.LastLoginAt,
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
