using ImpactX.Core.Domain;
using ImpactX.Core.Domain.Enums;
using ImpactX.Core.Interfaces.Repositories;
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
            "SELECT TOP 1 * FROM c WHERE c.status = 'Active' AND " +
            "(c.ownerUserId = @userId OR EXISTS(" +
            "SELECT VALUE m FROM m IN c.memberships " +
            "WHERE m.userId = @userId AND m.status = 'Active')) " +
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
