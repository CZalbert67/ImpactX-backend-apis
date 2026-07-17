using ImpactX.Core.Domain;
using ImpactX.Core.Exceptions;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Models.DTOs;

namespace ImpactX.Services;

public class PermissionService : IPermissionService
{
    private readonly IUsuarioRepository _usuarioRepository;

    public PermissionService(IUsuarioRepository usuarioRepository)
    {
        _usuarioRepository = usuarioRepository;
    }

    public async Task<PermisosDto> GetPermissionsAsync(Guid usuarioId)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(usuarioId)
            ?? throw new NotFoundException("Usuario no encontrado.");

        return MapToPermisosDto(usuario.Permisos) ?? new PermisosDto();
    }

    public async Task<PermisosPlataformaDto> UpdateMobilePermissionsAsync(Guid usuarioId, UpdatePermissionsRequest request)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(usuarioId)
            ?? throw new NotFoundException("Usuario no encontrado.");

        usuario.Permisos ??= new PermisosApp();
        usuario.Permisos.Mobile = MapToPermisosPlataforma(request);

        await _usuarioRepository.UpdateAsync(usuario);

        return MapToPermisosPlataformaDto(usuario.Permisos.Mobile) ?? new PermisosPlataformaDto();
    }

    public async Task<PermisosPlataformaDto> UpdateWebPermissionsAsync(Guid usuarioId, UpdatePermissionsRequest request)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(usuarioId)
            ?? throw new NotFoundException("Usuario no encontrado.");

        usuario.Permisos ??= new PermisosApp();
        usuario.Permisos.Web = MapToPermisosPlataforma(request);

        await _usuarioRepository.UpdateAsync(usuario);

        return MapToPermisosPlataformaDto(usuario.Permisos.Web) ?? new PermisosPlataformaDto();
    }

    private static PermisosDto? MapToPermisosDto(PermisosApp? p)
    {
        return p is null ? null : new PermisosDto
        {
            Mobile = MapToPermisosPlataformaDto(p.Mobile),
            Web = MapToPermisosPlataformaDto(p.Web),
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
        };
    }

    private static PermisosPlataforma MapToPermisosPlataforma(UpdatePermissionsRequest r) => new()
    {
        Ubicacion = r.Ubicacion,
        Notificaciones = r.Notificaciones,
        Camara = r.Camara,
        Microfono = r.Microfono,
        Sensores = r.Sensores,
        Bluetooth = r.Bluetooth,
    };
}
