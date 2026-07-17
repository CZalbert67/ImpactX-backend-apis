using ImpactX.Core.Exceptions;
using ImpactX.Models.DTOs;

namespace ImpactX.Services;

public interface IPermissionService
{
    Task<PermisosDto> GetPermissionsAsync(Guid usuarioId);
    Task<PermisosPlataformaDto> UpdateMobilePermissionsAsync(Guid usuarioId, UpdatePermissionsRequest request);
    Task<PermisosPlataformaDto> UpdateWebPermissionsAsync(Guid usuarioId, UpdatePermissionsRequest request);
}
