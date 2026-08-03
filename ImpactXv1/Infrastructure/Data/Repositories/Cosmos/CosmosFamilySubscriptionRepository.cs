using ImpactX.Core.Domain;
using ImpactX.Core.Domain.Enums;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Core.Pagination;
using Microsoft.Azure.Cosmos;

namespace ImpactX.Infrastructure.Data.Repositories.Cosmos;

public class CosmosFamilySubscriptionRepository : IFamilySubscriptionRepository
{
    private readonly Container _container;

    public CosmosFamilySubscriptionRepository(CosmosDbContext dbContext)
    {
        _container = dbContext.FamilySubscriptions;
    }

    public async Task<FamilySubscription?> GetByOwnerAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.ownerUserId = @ownerUserId " +
            "AND c.status != 'Cancelled' AND c.status != 'Expired' " +
            "ORDER BY c.updatedAtUtc DESC")
            .WithParameter("@ownerUserId", ownerUserId.ToString());

        return await ReadFirstAsync(
            query,
            CosmosPartitionKeys.For(ownerUserId),
            cancellationToken);
    }

    public async Task<FamilySubscription?> GetActiveByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE (c.status = 'Active' OR c.status = 'PastDue') AND " +
            "(c.ownerUserId = @userId OR EXISTS(" +
            "SELECT VALUE m FROM m IN c.memberships " +
            "WHERE m.userId = @userId AND m.status = 'Active') OR EXISTS(" +
            "SELECT VALUE i FROM i IN c.invitations " +
            "WHERE i.targetUserId = @userId " +
            "AND (i.status = 'Accepted' OR i.status = 'Consumed'))) " +
            "ORDER BY c.updatedAtUtc DESC")
            .WithParameter("@userId", userId.ToString());

        return await ReadFirstAsync(query, null, cancellationToken);
    }

    public async Task<FamilySubscription?> GetByPublicIdAsync(
        string publicSubscriptionId,
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.publicSubscriptionId = @publicSubscriptionId")
            .WithParameter("@publicSubscriptionId", publicSubscriptionId);

        return await ReadFirstAsync(query, null, cancellationToken);
    }

    public async Task<FamilySubscription?> GetByInvitationCodeHashAsync(
        string codeHash,
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE EXISTS(" +
            "SELECT VALUE i FROM i IN c.invitations " +
            "WHERE i.codeHash = @codeHash AND i.status = 'Pending')")
            .WithParameter("@codeHash", codeHash);

        return await ReadFirstAsync(query, null, cancellationToken);
    }

    public async Task<FamilySubscription?> GetByInvitationPublicIdAsync(
        string publicInvitationId,
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE EXISTS(" +
            "SELECT VALUE i FROM i IN c.invitations " +
            "WHERE i.publicInvitationId = @publicInvitationId)")
            .WithParameter("@publicInvitationId", publicInvitationId);

        return await ReadFirstAsync(query, null, cancellationToken);
    }

    public async Task<IReadOnlyList<FamilySubscription>> GetPendingInvitationsForTargetAsync(
        Guid userId,
        string username,
        string publicProfileId,
        string emailNormalized,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE (c.status = 'Active' OR c.status = 'PastDue') AND EXISTS(" +
            "SELECT VALUE i FROM i IN c.invitations WHERE i.status = 'Pending' " +
            "AND i.expiresAtUtc > @utcNow AND (" +
            "i.targetUserId = @userId OR i.targetUsername = @username " +
            "OR i.targetPublicProfileId = @publicProfileId " +
            "OR i.targetEmailNormalized = @emailNormalized)) " +
            "ORDER BY c.updatedAtUtc DESC")
            .WithParameter("@utcNow", utcNow.ToString("O"))
            .WithParameter("@userId", userId.ToString())
            .WithParameter("@username", username)
            .WithParameter("@publicProfileId", publicProfileId)
            .WithParameter("@emailNormalized", emailNormalized);

        var results = new List<FamilySubscription>();
        string? continuationToken = null;
        do
        {
            var page = await CosmosPageReader.ReadSinglePageAsync<FamilySubscription>(
                _container,
                query,
                null,
                PaginationDefaults.MaxPageSize,
                continuationToken,
                cancellationToken);
            results.AddRange(page.Items);
            continuationToken = page.ContinuationToken;
        } while (continuationToken is not null);

        return results;
    }

    public async Task AddAsync(
        FamilySubscription subscription,
        CancellationToken cancellationToken = default)
    {
        var response = await _container.CreateItemAsync(
            subscription,
            CosmosPartitionKeys.For(subscription.OwnerUserId),
            cancellationToken: cancellationToken);
        subscription.ETag = response.ETag;
    }

    public async Task UpdateAsync(
        FamilySubscription subscription,
        CancellationToken cancellationToken = default)
    {
        var options = string.IsNullOrWhiteSpace(subscription.ETag)
            ? null
            : new ItemRequestOptions { IfMatchEtag = subscription.ETag };

        var response = await _container.ReplaceItemAsync(
            subscription,
            subscription.Id.ToString(),
            CosmosPartitionKeys.For(subscription.OwnerUserId),
            options,
            cancellationToken);
        subscription.ETag = response.ETag;
    }

    public async Task<int> ProcessLifecycleAsync(
        DateTime utcNow,
        Func<FamilySubscription, CancellationToken, Task> process,
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE (c.status = 'Active' AND c.periodEndUtc <= @now) " +
            "OR (c.status = 'PastDue' AND IS_DEFINED(c.graceEndsAtUtc) " +
            "AND c.graceEndsAtUtc != null AND c.graceEndsAtUtc <= @now)")
            .WithParameter("@now", utcNow.ToString("O"));

        var processed = 0;
        string? continuationToken = null;
        do
        {
            var page = await CosmosPageReader.ReadSinglePageAsync<FamilySubscription>(
                _container,
                query,
                null,
                PaginationDefaults.MaxPageSize,
                continuationToken,
                cancellationToken);

            foreach (var subscription in page.Items)
            {
                await process(subscription, cancellationToken);
                processed++;
            }

            continuationToken = page.ContinuationToken;
        } while (continuationToken is not null);

        return processed;
    }

    private async Task<FamilySubscription?> ReadFirstAsync(
        QueryDefinition query,
        PartitionKey? partitionKey,
        CancellationToken cancellationToken)
    {
        var requestOptions = new QueryRequestOptions { MaxItemCount = 1 };
        if (partitionKey.HasValue)
        {
            requestOptions.PartitionKey = partitionKey.Value;
        }

        using var iterator = _container.GetItemQueryIterator<FamilySubscription>(
            query,
            requestOptions: requestOptions);

        if (!iterator.HasMoreResults)
        {
            return null;
        }

        var response = await iterator.ReadNextAsync(cancellationToken);
        return response.FirstOrDefault();
    }
}
