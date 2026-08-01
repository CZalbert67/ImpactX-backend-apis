using ImpactX.Core.Domain;
using ImpactX.Core.Pagination;

namespace ImpactX.Core.Interfaces.Repositories;

public interface IContactoRepository
{
    Task<List<ContactoEmergencia>> GetByUserAsync(Guid usuarioId);
    Task<PagedResult<ContactoEmergencia>> GetByUserPagedAsync(Guid usuarioId, int pageSize, string? continuationToken, CancellationToken cancellationToken = default);
    Task<ContactoEmergencia?> GetByIdAsync(Guid id);
    Task<ContactoEmergencia?> GetByIdAsync(Guid usuarioId, Guid id);
    Task<ContactoEmergencia?> GetPrincipalAsync(Guid usuarioId);
    Task<int> CountByUserAsync(Guid usuarioId);
    Task<bool> ExistsByTelefonoAsync(Guid usuarioId, string telefono);
    Task AddAsync(ContactoEmergencia contacto);
    Task UpdateAsync(ContactoEmergencia contacto);
    Task DeleteAsync(ContactoEmergencia contacto);
}
