using ImpactX.Core.Domain;
using ImpactX.Core.Domain.Enums;
using ImpactX.Core.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ImpactX.Infrastructure.Data.Repositories.EF;

public class FamilySubscriptionRepository : IFamilySubscriptionRepository
{
    private readonly ApplicationDbContext _context;

    public FamilySubscriptionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<FamilySubscription?> GetByOwnerAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default)
    {
        return _context.FamilySubscriptions
            .OrderByDescending(subscription => subscription.UpdatedAtUtc)
            .FirstOrDefaultAsync(
                subscription => subscription.OwnerUserId == ownerUserId
                    && subscription.Status != FamilySubscriptionStatus.Cancelled
                    && subscription.Status != FamilySubscriptionStatus.Expired,
                cancellationToken);
    }

    public Task<FamilySubscription?> GetActiveByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return _context.FamilySubscriptions
            .OrderByDescending(subscription => subscription.UpdatedAtUtc)
            .FirstOrDefaultAsync(
                subscription => subscription.Status == FamilySubscriptionStatus.Active
                    && (subscription.OwnerUserId == userId
                        || subscription.Memberships.Any(membership =>
                            membership.UserId == userId
                            && membership.Status == FamilyMembershipStatus.Active)),
                cancellationToken);
    }

    public Task<FamilySubscription?> GetByPublicIdAsync(
        string publicSubscriptionId,
        CancellationToken cancellationToken = default)
    {
        return _context.FamilySubscriptions.FirstOrDefaultAsync(
            subscription => subscription.PublicSubscriptionId == publicSubscriptionId,
            cancellationToken);
    }

    public Task<FamilySubscription?> GetByInvitationCodeHashAsync(
        string codeHash,
        CancellationToken cancellationToken = default)
    {
        return _context.FamilySubscriptions
            .OrderByDescending(subscription => subscription.UpdatedAtUtc)
            .FirstOrDefaultAsync(
                subscription => subscription.Invitations.Any(invitation =>
                    invitation.CodeHash == codeHash
                    && invitation.Status == FamilyInvitationStatus.Pending),
                cancellationToken);
    }

    public Task<FamilySubscription?> GetByInvitationPublicIdAsync(
        string publicInvitationId,
        CancellationToken cancellationToken = default)
    {
        return _context.FamilySubscriptions
            .OrderByDescending(subscription => subscription.UpdatedAtUtc)
            .FirstOrDefaultAsync(
                subscription => subscription.Invitations.Any(invitation =>
                    invitation.PublicInvitationId == publicInvitationId),
                cancellationToken);
    }

    public async Task AddAsync(
        FamilySubscription subscription,
        CancellationToken cancellationToken = default)
    {
        await _context.FamilySubscriptions.AddAsync(subscription, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        FamilySubscription subscription,
        CancellationToken cancellationToken = default)
    {
        if (_context.Entry(subscription).State != EntityState.Detached)
        {
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        var tracked = _context.FamilySubscriptions.Local
            .FirstOrDefault(value => value.Id == subscription.Id);

        tracked ??= await _context.FamilySubscriptions
            .Include(value => value.Memberships)
            .Include(value => value.Invitations)
            .Include(value => value.Payments)
            .SingleOrDefaultAsync(value => value.Id == subscription.Id, cancellationToken);

        if (tracked is null)
        {
            throw new DbUpdateConcurrencyException(
                "La suscripción familiar ya no existe o fue modificada.");
        }

        _context.Entry(tracked).CurrentValues.SetValues(subscription);
        SynchronizeOwnedCollection(
            tracked.Memberships,
            subscription.Memberships,
            value => value.Id);
        SynchronizeOwnedCollection(
            tracked.Invitations,
            subscription.Invitations,
            value => value.Id);
        SynchronizeOwnedCollection(
            tracked.Payments,
            subscription.Payments,
            value => value.Id);

        await _context.SaveChangesAsync(cancellationToken);
    }

    private void SynchronizeOwnedCollection<T>(
        ICollection<T> trackedValues,
        IEnumerable<T> incomingValues,
        Func<T, Guid> idSelector)
        where T : class
    {
        var incomingById = incomingValues.ToDictionary(idSelector);

        foreach (var trackedValue in trackedValues.ToList())
        {
            var id = idSelector(trackedValue);
            if (!incomingById.Remove(id, out var incomingValue))
            {
                trackedValues.Remove(trackedValue);
                continue;
            }

            _context.Entry(trackedValue).CurrentValues.SetValues(incomingValue);
        }

        foreach (var incomingValue in incomingById.Values)
        {
            trackedValues.Add(incomingValue);
        }
    }
}
