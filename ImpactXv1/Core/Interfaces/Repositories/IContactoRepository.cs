using ImpactX.Core.Domain;
using ImpactX.Core.Pagination;

namespace ImpactX.Core.Interfaces.Repositories;

public interface IContactoRepository
{
    Task<List<ContactoEmergencia>> GetByUserAsync(Guid usuarioId);

    Task<PagedResult<ContactoEmergencia>> GetV1ForUserPagedAsync(
        Guid userId,
        string emailNormalized,
        int pageSize,
        string? continuationToken,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContactoEmergencia>> GetV1ForUserAsync(
        Guid userId,
        string emailNormalized,
        CancellationToken cancellationToken = default);

    Task<ContactoEmergencia?> GetByPublicIdAsync(
        string publicContactId,
        CancellationToken cancellationToken = default);

    Task<ContactoEmergencia?> GetByInvitationCodeHashAsync(
        string codeHash,
        CancellationToken cancellationToken = default);

    Task<int> CountAcceptedByOwnerAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default);

    Task<ContactoEmergencia?> GetAcceptedPrimaryByOwnerAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsV1ActiveOrPendingAsync(
        Guid ownerUserId,
        Guid? contactUserId,
        string? targetEmailNormalized,
        Guid? excludeContactId = null,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsV1BlockedAsync(
        Guid ownerUserId,
        string ownerEmailNormalized,
        Guid? contactUserId,
        string? targetEmailNormalized,
        CancellationToken cancellationToken = default);
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
