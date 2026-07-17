using Microsoft.Azure.Cosmos;
using Monitor = ImpactX.Core.Domain.Monitor;
using ImpactX.Core.Interfaces.Repositories;

namespace ImpactX.Infrastructure.Data.Repositories.Cosmos;

public class CosmosMonitorRepository : IMonitorRepository
{
    private readonly Container _container;

    public CosmosMonitorRepository(CosmosDbContext dbContext)
    {
        _container = dbContext.Monitores;
    }

    public async Task<List<Monitor>> GetByUserAsync(Guid usuarioId)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.usuarioId = @usuarioId ORDER BY c.creadoEn DESC")
            .WithParameter("@usuarioId", usuarioId.ToString());

        var results = new List<Monitor>();
        using var iterator = _container.GetItemQueryIterator<Monitor>(query);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }
        return results;
    }

    public async Task<Monitor?> GetByIdAsync(Guid id)
    {
        try
        {
            var response = await _container.ReadItemAsync<Monitor>(
                id.ToString(), new PartitionKey(id.ToString()));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<List<Monitor>> GetActiveByUserAsync(Guid usuarioId)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.usuarioId = @usuarioId AND c.estado = 'Activo'")
            .WithParameter("@usuarioId", usuarioId.ToString());

        var results = new List<Monitor>();
        using var iterator = _container.GetItemQueryIterator<Monitor>(query);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }
        return results;
    }

    public async Task<int> CountActiveByUserAsync(Guid usuarioId)
    {
        var query = new QueryDefinition(
            "SELECT VALUE COUNT(1) FROM c WHERE c.usuarioId = @usuarioId AND c.estado = 'Activo'")
            .WithParameter("@usuarioId", usuarioId.ToString());

        using var iterator = _container.GetItemQueryIterator<int>(query);
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }
        return 0;
    }

    public async Task<Monitor?> GetByTokenAsync(string token)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.tokenInvitacion = @token")
            .WithParameter("@token", token);

        using var iterator = _container.GetItemQueryIterator<Monitor>(query);
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }
        return null;
    }

    public async Task<Monitor?> GetByUsuarioYMonitorAsync(Guid usuarioId, Guid monitorUsuarioId)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.usuarioId = @usuarioId AND c.profileId = @monitorId")
            .WithParameter("@usuarioId", usuarioId.ToString())
            .WithParameter("@monitorId", monitorUsuarioId.ToString());

        using var iterator = _container.GetItemQueryIterator<Monitor>(query);
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }
        return null;
    }

    public async Task<bool> ExistsByUsernameAsync(Guid usuarioId, string username)
    {
        var query = new QueryDefinition(
            "SELECT VALUE COUNT(1) FROM c WHERE c.usuarioId = @usuarioId AND c.username = @username AND c.estado != 'Revocado'")
            .WithParameter("@usuarioId", usuarioId.ToString())
            .WithParameter("@username", username);

        using var iterator = _container.GetItemQueryIterator<int>(query);
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault() > 0;
        }
        return false;
    }

    public async Task AddAsync(Monitor monitor)
    {
        monitor.Id = Guid.NewGuid();
        await _container.CreateItemAsync(monitor, new PartitionKey(monitor.UsuarioId.ToString()));
    }

    public async Task UpdateAsync(Monitor monitor)
    {
        await _container.UpsertItemAsync(monitor, new PartitionKey(monitor.UsuarioId.ToString()));
    }

    public async Task DeleteAsync(Monitor monitor)
    {
        await _container.DeleteItemAsync<Monitor>(
            monitor.Id.ToString(),
            new PartitionKey(monitor.UsuarioId.ToString()));
    }
}
