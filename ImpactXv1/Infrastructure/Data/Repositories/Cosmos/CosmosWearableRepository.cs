using Microsoft.Azure.Cosmos;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Infrastructure.Data;

namespace ImpactX.Infrastructure.Data.Repositories.Cosmos;

public class CosmosWearableRepository : IWearableRepository
{
    private readonly Container _container;

    public CosmosWearableRepository(CosmosDbContext dbContext)
    {
        _container = dbContext.Wearables;
    }

    public async Task<Wearable?> GetByIdAsync(Guid id)
    {
        // Cross-partition justificada: el contrato solo recibe el id y
        // Wearables particiona por /usuarioId. Corrige el ReadItemAsync
        // anterior con partition key incorrecta que siempre devolvía 404.
        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.id = @id")
            .WithParameter("@id", id.ToString());

        using var iterator = _container.GetItemQueryIterator<Wearable>(query,
            requestOptions: new QueryRequestOptions { MaxItemCount = 1 });
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }
        return null;
    }

    public async Task<Wearable?> GetByUsuarioIdAsync(Guid usuarioId)
    {
        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.usuarioId = @usuarioId AND c.estado = 'Vinculado'")
            .WithParameter("@usuarioId", usuarioId.ToString());

        using var iterator = _container.GetItemQueryIterator<Wearable>(query,
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
        return null;
    }

    public async Task<List<Wearable>> GetAllByUsuarioIdAsync(Guid usuarioId)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.usuarioId = @usuarioId")
            .WithParameter("@usuarioId", usuarioId.ToString());

        var results = new List<Wearable>();
        using var iterator = _container.GetItemQueryIterator<Wearable>(query,
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

    public async Task<Wearable?> GetByPairingTokenAsync(string token)
    {
        // Cross-partition justificada: búsqueda por pairing token sin
        // usuarioId conocido; la partición es /usuarioId. Detención temprana.
        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.pairingToken = @token")
            .WithParameter("@token", token);

        using var iterator = _container.GetItemQueryIterator<Wearable>(query,
            requestOptions: new QueryRequestOptions { MaxItemCount = 1 });
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }
        return null;
    }

    public async Task<Wearable?> GetByDispositivoIdAsync(string dispositivoId)
    {
        // Cross-partition justificada: búsqueda por dispositivoId sin
        // usuarioId conocido; la partición es /usuarioId. Detención temprana.
        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.dispositivoId = @dispositivoId")
            .WithParameter("@dispositivoId", dispositivoId);

        using var iterator = _container.GetItemQueryIterator<Wearable>(query,
            requestOptions: new QueryRequestOptions { MaxItemCount = 1 });
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
        await _container.CreateItemAsync(wearable,
            CosmosPartitionKeys.For(wearable.UsuarioId));
    }

    public async Task UpdateAsync(Wearable wearable)
    {
        await _container.ReplaceItemAsync(wearable,
            wearable.Id.ToString(),
            CosmosPartitionKeys.For(wearable.UsuarioId));
    }

    public async Task DeleteAsync(Wearable wearable)
    {
        await _container.DeleteItemAsync<Wearable>(
            wearable.Id.ToString(),
            CosmosPartitionKeys.For(wearable.UsuarioId));
    }
}
