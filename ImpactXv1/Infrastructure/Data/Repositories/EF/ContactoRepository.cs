using ImpactX.Core.Domain;
using ImpactX.Core.Domain.Enums;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Core.Pagination;
using Microsoft.EntityFrameworkCore;

namespace ImpactX.Infrastructure.Data.Repositories.EF;

public class ContactoRepository : IContactoRepository
{
    private readonly ApplicationDbContext _context;

    public ContactoRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ContactoEmergencia>> GetByUserAsync(Guid usuarioId)
    {
        return await LegacyQuery(usuarioId)
            .OrderByDescending(c => c.EsPrincipal)
            .ThenBy(c => c.CreadoEn)
            .ToListAsync();
    }

    public Task<PagedResult<ContactoEmergencia>> GetByUserPagedAsync(
        Guid usuarioId,
        int pageSize,
        string? continuationToken,
        CancellationToken cancellationToken = default)
    {
        return EfPageReader.ReadSinglePageAsync(
            LegacyQuery(usuarioId)
                .OrderByDescending(c => c.EsPrincipal)
                .ThenBy(c => c.CreadoEn),
            pageSize,
            continuationToken,
            cancellationToken);
    }

    public Task<PagedResult<ContactoEmergencia>> GetV1ForUserPagedAsync(
        Guid userId,
        string emailNormalized,
        int pageSize,
        string? continuationToken,
        CancellationToken cancellationToken = default)
    {
        return EfPageReader.ReadSinglePageAsync(
            V1ParticipantQuery(userId, emailNormalized)
                .OrderByDescending(c => c.UpdatedAtUtc ?? c.RequestedAtUtc ?? c.CreadoEn),
            pageSize,
            continuationToken,
            cancellationToken);
    }

    public async Task<IReadOnlyList<ContactoEmergencia>> GetV1ForUserAsync(
        Guid userId,
        string emailNormalized,
        CancellationToken cancellationToken = default)
    {
        return await V1ParticipantQuery(userId, emailNormalized)
            .OrderByDescending(c => c.UpdatedAtUtc ?? c.RequestedAtUtc ?? c.CreadoEn)
            .ToListAsync(cancellationToken);
    }

    public Task<ContactoEmergencia?> GetByPublicIdAsync(
        string publicContactId,
        CancellationToken cancellationToken = default)
    {
        return _context.ContactosEmergencia.FirstOrDefaultAsync(
            contact => contact.PublicContactId == publicContactId,
            cancellationToken);
    }

    public Task<ContactoEmergencia?> GetByInvitationCodeHashAsync(
        string codeHash,
        CancellationToken cancellationToken = default)
    {
        return _context.ContactosEmergencia.FirstOrDefaultAsync(
            contact => contact.InvitationCodeHash == codeHash,
            cancellationToken);
    }

    public Task<int> CountAcceptedByOwnerAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default)
    {
        return _context.ContactosEmergencia.CountAsync(
            contact => contact.UsuarioId == ownerUserId
                && contact.PublicContactId != null
                && contact.Status == EmergencyContactStatus.Accepted,
            cancellationToken);
    }

    public Task<ContactoEmergencia?> GetAcceptedPrimaryByOwnerAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default)
    {
        return _context.ContactosEmergencia.FirstOrDefaultAsync(
            contact => contact.UsuarioId == ownerUserId
                && contact.PublicContactId != null
                && contact.Status == EmergencyContactStatus.Accepted
                && contact.EsPrincipal,
            cancellationToken);
    }

    public Task<bool> ExistsV1ActiveOrPendingAsync(
        Guid ownerUserId,
        Guid? contactUserId,
        string? targetEmailNormalized,
        Guid? excludeContactId = null,
        CancellationToken cancellationToken = default)
    {
        return _context.ContactosEmergencia.AnyAsync(
            contact => contact.UsuarioId == ownerUserId
                && contact.PublicContactId != null
                && (contact.Status == EmergencyContactStatus.Pending
                    || contact.Status == EmergencyContactStatus.Accepted)
                && (!excludeContactId.HasValue || contact.Id != excludeContactId.Value)
                && ((contactUserId.HasValue && contact.ContactUserId == contactUserId)
                    || (!string.IsNullOrEmpty(targetEmailNormalized)
                        && contact.TargetEmailNormalized == targetEmailNormalized)),
            cancellationToken);
    }

    public Task<bool> ExistsV1BlockedAsync(
        Guid ownerUserId,
        string ownerEmailNormalized,
        Guid? contactUserId,
        string? targetEmailNormalized,
        CancellationToken cancellationToken = default)
    {
        return _context.ContactosEmergencia.AnyAsync(
            contact => contact.PublicContactId != null
                && contact.Status == EmergencyContactStatus.Blocked
                && ((contact.UsuarioId == ownerUserId
                        && ((contactUserId.HasValue && contact.ContactUserId == contactUserId)
                            || (!string.IsNullOrEmpty(targetEmailNormalized)
                                && contact.TargetEmailNormalized == targetEmailNormalized)))
                    || (contactUserId.HasValue
                        && contact.UsuarioId == contactUserId.Value
                        && (contact.ContactUserId == ownerUserId
                            || (!string.IsNullOrEmpty(ownerEmailNormalized)
                                && contact.TargetEmailNormalized == ownerEmailNormalized)))),
            cancellationToken);
    }

    public Task<ContactoEmergencia?> GetByIdAsync(Guid id)
    {
        return _context.ContactosEmergencia.FirstOrDefaultAsync(
            contact => contact.Id == id &&
                (contact.PublicContactId == null || contact.PublicContactId == string.Empty));
    }

    public Task<ContactoEmergencia?> GetByIdAsync(Guid usuarioId, Guid id)
    {
        return LegacyQuery(usuarioId).FirstOrDefaultAsync(c => c.Id == id);
    }

    public Task<ContactoEmergencia?> GetPrincipalAsync(Guid usuarioId)
    {
        return LegacyQuery(usuarioId).FirstOrDefaultAsync(c => c.EsPrincipal);
    }

    public Task<int> CountByUserAsync(Guid usuarioId)
    {
        return LegacyQuery(usuarioId).CountAsync();
    }

    public Task<bool> ExistsByTelefonoAsync(Guid usuarioId, string telefono)
    {
        return LegacyQuery(usuarioId).AnyAsync(c => c.Telefono == telefono);
    }

    public async Task AddAsync(ContactoEmergencia contacto)
    {
        await _context.ContactosEmergencia.AddAsync(contacto);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(ContactoEmergencia contacto)
    {
        _context.ContactosEmergencia.Update(contacto);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(ContactoEmergencia contacto)
    {
        _context.ContactosEmergencia.Remove(contacto);
        await _context.SaveChangesAsync();
    }

    private IQueryable<ContactoEmergencia> LegacyQuery(Guid usuarioId)
    {
        return _context.ContactosEmergencia.Where(contact =>
            contact.UsuarioId == usuarioId
            && (contact.PublicContactId == null || contact.PublicContactId == string.Empty));
    }

    private IQueryable<ContactoEmergencia> V1ParticipantQuery(
        Guid userId,
        string emailNormalized)
    {
        return _context.ContactosEmergencia.Where(contact =>
            contact.PublicContactId != null
            && (contact.UsuarioId == userId
                || contact.ContactUserId == userId
                || (contact.Status == EmergencyContactStatus.Pending
                    && contact.TargetEmailNormalized == emailNormalized)));
    }
}
