using ImpactX.Core.Domain;

namespace ImpactX.Core.Interfaces.Repositories;

public interface IFamilySubscriptionRepository
{
    Task<FamilySubscription?> GetByOwnerAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default);

    Task<FamilySubscription?> GetActiveByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<FamilySubscription?> GetByPublicIdAsync(
        string publicSubscriptionId,
        CancellationToken cancellationToken = default);

    Task<FamilySubscription?> GetByInvitationCodeHashAsync(
        string codeHash,
        CancellationToken cancellationToken = default);

    Task<FamilySubscription?> GetByInvitationPublicIdAsync(
        string publicInvitationId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        FamilySubscription subscription,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        FamilySubscription subscription,
        CancellationToken cancellationToken = default);

    Task<int> ProcessLifecycleAsync(
        DateTime utcNow,
        Func<FamilySubscription, CancellationToken, Task> process,
        CancellationToken cancellationToken = default);
}
