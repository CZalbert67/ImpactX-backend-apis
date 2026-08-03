using ImpactX.Core.Domain;
using ImpactX.Core.Domain.Enums;
using ImpactX.Core.Interfaces.Repositories;
using Microsoft.Azure.Cosmos;

namespace ImpactX.Infrastructure.Data.Repositories.Cosmos;

public class CosmosMonitoringRelationshipRepository : IMonitoringRelationshipRepository
{
    private readonly Container _container;

    public CosmosMonitoringRelationshipRepository(CosmosDbContext dbContext)
    {
        _container = dbContext.MonitoringRelationships;
    }

    public async Task<MonitoringRelationship?> GetByPublicIdAsync(
        string publicRelationshipId,
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.publicRelationshipId = @publicRelationshipId")
            .WithParameter("@publicRelationshipId", publicRelationshipId);
        return await ReadFirstAsync(query, null, cancellationToken);
    }

    public async Task<MonitoringRelationship?> GetByInvitationCodeHashAsync(
        string codeHash,
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.invitationCodeHash = @codeHash " +
            "AND c.status = 'Pending'")
            .WithParameter("@codeHash", codeHash);
        return await ReadFirstAsync(query, null, cancellationToken);
    }

    public async Task<IReadOnlyList<MonitoringRelationship>> GetForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.monitorUserId = @userId OR c.monitoredUserId = @userId")
            .WithParameter("@userId", userId.ToString());

        var results = new List<MonitoringRelationship>();
        using var iterator = _container.GetItemQueryIterator<MonitoringRelationship>(
            query,
            requestOptions: new QueryRequestOptions { MaxItemCount = 100 });
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response);
        }

        return results.OrderByDescending(value => value.UpdatedAtUtc).ToList();
    }

    public async Task<int> CountAcceptedByMonitorAsync(
        Guid monitorUserId,
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT VALUE COUNT(1) FROM c WHERE c.monitorUserId = @monitorUserId " +
            "AND c.status = 'Accepted'")
            .WithParameter("@monitorUserId", monitorUserId.ToString());
        using var iterator = _container.GetItemQueryIterator<int>(
            query,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = CosmosPartitionKeys.For(monitorUserId),
                MaxItemCount = 1
            });
        if (!iterator.HasMoreResults)
        {
            return 0;
        }

        var response = await iterator.ReadNextAsync(cancellationToken);
        return response.FirstOrDefault();
    }

    public async Task<IReadOnlyList<MonitoringRelationship>> GetAcceptedForMonitoredUserAsync(
        Guid monitoredUserId,
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.monitoredUserId = @monitoredUserId " +
            "AND c.status = 'Accepted'")
            .WithParameter("@monitoredUserId", monitoredUserId.ToString());

        var results = new List<MonitoringRelationship>();
        using var iterator = _container.GetItemQueryIterator<MonitoringRelationship>(
            query,
            requestOptions: new QueryRequestOptions { MaxItemCount = 100 });
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response);
        }

        return results
            .OrderBy(value => value.AcceptedAtUtc)
            .ToList();
    }

    public async Task<bool> ExistsBlockedAsync(
        Guid monitorUserId,
        Guid monitoredUserId,
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT VALUE COUNT(1) FROM c WHERE c.monitorUserId = @monitorUserId " +
            "AND c.monitoredUserId = @monitoredUserId AND c.status = 'Blocked'")
            .WithParameter("@monitorUserId", monitorUserId.ToString())
            .WithParameter("@monitoredUserId", monitoredUserId.ToString());
        using var iterator = _container.GetItemQueryIterator<int>(
            query,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = CosmosPartitionKeys.For(monitorUserId),
                MaxItemCount = 1
            });
        if (!iterator.HasMoreResults)
        {
            return false;
        }

        var response = await iterator.ReadNextAsync(cancellationToken);
        return response.FirstOrDefault() > 0;
    }

    public async Task<bool> ExistsActiveOrPendingAsync(
        Guid monitorUserId,
        Guid monitoredUserId,
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT VALUE COUNT(1) FROM c WHERE c.monitorUserId = @monitorUserId " +
            "AND c.monitoredUserId = @monitoredUserId " +
            "AND (c.status = 'Pending' OR c.status = 'Accepted')")
            .WithParameter("@monitorUserId", monitorUserId.ToString())
            .WithParameter("@monitoredUserId", monitoredUserId.ToString());
        using var iterator = _container.GetItemQueryIterator<int>(
            query,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = CosmosPartitionKeys.For(monitorUserId),
                MaxItemCount = 1
            });
        if (!iterator.HasMoreResults)
        {
            return false;
        }

        var response = await iterator.ReadNextAsync(cancellationToken);
        return response.FirstOrDefault() > 0;
    }

    public async Task AddAsync(
        MonitoringRelationship relationship,
        CancellationToken cancellationToken = default)
    {
        var response = await _container.CreateItemAsync(
            relationship,
            CosmosPartitionKeys.For(relationship.MonitorUserId),
            cancellationToken: cancellationToken);
        relationship.ETag = response.ETag;
    }

    public async Task UpdateAsync(
        MonitoringRelationship relationship,
        CancellationToken cancellationToken = default)
    {
        var options = string.IsNullOrWhiteSpace(relationship.ETag)
            ? null
            : new ItemRequestOptions { IfMatchEtag = relationship.ETag };
        var response = await _container.ReplaceItemAsync(
            relationship,
            relationship.Id.ToString(),
            CosmosPartitionKeys.For(relationship.MonitorUserId),
            options,
            cancellationToken);
        relationship.ETag = response.ETag;
    }

    private async Task<MonitoringRelationship?> ReadFirstAsync(
        QueryDefinition query,
        PartitionKey? partitionKey,
        CancellationToken cancellationToken)
    {
        var options = new QueryRequestOptions { MaxItemCount = 1 };
        if (partitionKey.HasValue)
        {
            options.PartitionKey = partitionKey.Value;
        }

        using var iterator = _container.GetItemQueryIterator<MonitoringRelationship>(
            query,
            requestOptions: options);
        if (!iterator.HasMoreResults)
        {
            return null;
        }

        var response = await iterator.ReadNextAsync(cancellationToken);
        return response.FirstOrDefault();
    }
}
