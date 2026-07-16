using Prueba1.Models.DTOs;

namespace Prueba1.Services;

public interface IPermissionService
{
    Task<PermisosDto> GetPermissionsAsync(Guid usuarioId);
    Task<PermisosPlataformaDto> UpdateMobilePermissionsAsync(Guid usuarioId, UpdatePermissionsRequest request);
    Task<PermisosPlataformaDto> UpdateWebPermissionsAsync(Guid usuarioId, UpdatePermissionsRequest request);
}
