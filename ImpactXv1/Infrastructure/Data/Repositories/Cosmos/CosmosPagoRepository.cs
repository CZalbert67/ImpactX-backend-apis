using Microsoft.Azure.Cosmos;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Infrastructure.Data;

namespace ImpactX.Infrastructure.Data.Repositories.Cosmos;

public class CosmosPagoRepository : IPagoRepository
{
    private readonly Container _container;

    public CosmosPagoRepository(CosmosDbContext dbContext)
    {
        _container = dbContext.Pagos;
    }

    public async Task<List<Pago>> GetByUserAsync(Guid usuarioId)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.usuarioId = @usuarioId ORDER BY c.fechaPago DESC")
            .WithParameter("@usuarioId", usuarioId.ToString());

        var list = new List<Pago>();
        using var iterator = _container.GetItemQueryIterator<Pago>(query,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = CosmosPartitionKeys.For(usuarioId),
                MaxItemCount = 100
            });
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            list.AddRange(response);
        }
        return list;
    }

    public async Task<Pago?> GetByIdAsync(Guid id)
    {
        // Cross-partition justificada: el contrato solo recibe el id y Pagos
        // particiona por /usuarioId; no hay partition key disponible.
        // Detención temprana.
        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.id = @id")
            .WithParameter("@id", id.ToString());

        using var iterator = _container.GetItemQueryIterator<Pago>(query,
            requestOptions: new QueryRequestOptions { MaxItemCount = 1 });
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }
        return null;
    }

    public async Task AddAsync(Pago pago)
    {
        pago.Id = Guid.NewGuid();
        await _container.CreateItemAsync(pago,
            CosmosPartitionKeys.For(pago.UsuarioId));
    }

    public async Task UpdateAsync(Pago pago)
    {
        await _container.ReplaceItemAsync(pago,
            pago.Id.ToString(),
            CosmosPartitionKeys.For(pago.UsuarioId));
    }
}
