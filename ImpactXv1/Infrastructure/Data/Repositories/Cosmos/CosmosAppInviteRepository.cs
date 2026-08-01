using Microsoft.Azure.Cosmos;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Infrastructure.Data;

namespace ImpactX.Infrastructure.Data.Repositories.Cosmos;

public class CosmosAppInviteRepository : IAppInviteRepository
{
    private readonly Container _container;

    public CosmosAppInviteRepository(CosmosDbContext dbContext)
    {
        _container = dbContext.AppInvites;
    }

    public async Task<List<AppInvite>> GetByUserAsync(Guid usuarioId)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.usuarioId = @usuarioId ORDER BY c.createdAt DESC")
            .WithParameter("@usuarioId", usuarioId.ToString());

        var results = new List<AppInvite>();
        using var iterator = _container.GetItemQueryIterator<AppInvite>(query,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = CosmosPartitionKeys.For(usuarioId),
                MaxItemCount = 100
            });
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }
        return results;
    }

    public async Task<AppInvite?> GetByIdAsync(Guid id)
    {
        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.id = @id")
            .WithParameter("@id", id.ToString());

        using var iterator = _container.GetItemQueryIterator<AppInvite>(query,
            requestOptions: new QueryRequestOptions { MaxItemCount = 1 });
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }
        return null;
    }

    public async Task<AppInvite?> GetByTokenAsync(string token)
    {
        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.token = @token")
            .WithParameter("@token", token);

        using var iterator = _container.GetItemQueryIterator<AppInvite>(query,
            requestOptions: new QueryRequestOptions { MaxItemCount = 1 });
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }
        return null;
    }

    public async Task<int> CountPendingByUserAsync(Guid usuarioId)
    {
        var query = new QueryDefinition(
            "SELECT VALUE COUNT(1) FROM c WHERE c.usuarioId = @usuarioId AND c.status = @status")
            .WithParameter("@usuarioId", usuarioId.ToString())
            .WithParameter("@status", "Pendiente de registro");

        using var iterator = _container.GetItemQueryIterator<int>(query,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = CosmosPartitionKeys.For(usuarioId),
                MaxItemCount = 1
            });
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }
        return 0;
    }

    public async Task AddAsync(AppInvite invite)
    {
        invite.Id = Guid.NewGuid();
        await _container.CreateItemAsync(invite,
            CosmosPartitionKeys.For(invite.UsuarioId));
    }

    public async Task UpdateAsync(AppInvite invite)
    {
        await _container.ReplaceItemAsync(invite,
            invite.Id.ToString(),
            CosmosPartitionKeys.For(invite.UsuarioId));
    }

    public async Task DeleteAsync(AppInvite invite)
    {
        await _container.DeleteItemAsync<AppInvite>(
            invite.Id.ToString(),
            CosmosPartitionKeys.For(invite.UsuarioId));
    }
}
