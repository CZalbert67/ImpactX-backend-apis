using Microsoft.Azure.Cosmos;
using Prueba1.Core.Domain;
using Prueba1.Core.Interfaces.Repositories;

namespace Prueba1.Infrastructure.Data.Repositories.Cosmos;

public class CosmosWearableRepository : IWearableRepository
{
    private readonly Container _container;

    public CosmosWearableRepository(CosmosDbContext dbContext)
    {
        _container = dbContext.Wearables;
    }

    public async Task<Wearable?> GetByIdAsync(Guid id)
    {
        try
        {
            var response = await _container.ReadItemAsync<Wearable>(
                id.ToString(), new PartitionKey(id.ToString()));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<Wearable?> GetByUsuarioIdAsync(Guid usuarioId)
    {
        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.usuarioId = @usuarioId AND c.estado = 'Vinculado'")
            .WithParameter("@usuarioId", usuarioId.ToString());

        using var iterator = _container.GetItemQueryIterator<Wearable>(query);
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }
        return null;
    }

    public async Task<List<Wearable>> GetAllByUsuarioIdAsync(Guid usuarioId)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.usuarioId = @usuarioId")
            .WithParameter("@usuarioId", usuarioId.ToString());

        var results = new List<Wearable>();
        using var iterator = _container.GetItemQueryIterator<Wearable>(query);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }
        return results;
    }

    public async Task<Wearable?> GetByPairingTokenAsync(string token)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.pairingToken = @token")
            .WithParameter("@token", token);

        using var iterator = _container.GetItemQueryIterator<Wearable>(query);
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }
        return null;
    }

    public async Task<Wearable?> GetByDispositivoIdAsync(string dispositivoId)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.dispositivoId = @dispositivoId")
            .WithParameter("@dispositivoId", dispositivoId);

        using var iterator = _container.GetItemQueryIterator<Wearable>(query);
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }
        return null;
    }

    public async Task AddAsync(Wearable wearable)
    {
        wearable.Id = Guid.NewGuid();
        await _container.CreateItemAsync(wearable, new PartitionKey(wearable.UsuarioId.ToString()));
    }

    public async Task UpdateAsync(Wearable wearable)
    {
        await _container.UpsertItemAsync(wearable, new PartitionKey(wearable.UsuarioId.ToString()));
    }

    public async Task DeleteAsync(Wearable wearable)
    {
        await _container.DeleteItemAsync<Wearable>(
            wearable.Id.ToString(),
            new PartitionKey(wearable.UsuarioId.ToString()));
    }
}
