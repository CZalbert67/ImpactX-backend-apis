using Microsoft.Azure.Cosmos;
using Prueba1.Core.Domain;
using Prueba1.Core.Interfaces.Repositories;

namespace Prueba1.Infrastructure.Data.Repositories.Cosmos;

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
        using var iterator = _container.GetItemQueryIterator<Pago>(query);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            list.AddRange(response);
        }
        return list;
    }

    public async Task<Pago?> GetByIdAsync(Guid id)
    {
        try
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @id")
                .WithParameter("@id", id.ToString());
            using var iterator = _container.GetItemQueryIterator<Pago>(query);
            if (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                return response.FirstOrDefault();
            }
            return null;
        }
        catch (CosmosException) { return null; }
    }

    public async Task AddAsync(Pago pago)
    {
        pago.Id = Guid.NewGuid();
        await _container.CreateItemAsync(pago,
            new PartitionKey(pago.UsuarioId.ToString()));
    }

    public async Task UpdateAsync(Pago pago)
    {
        await _container.UpsertItemAsync(pago,
            new PartitionKey(pago.UsuarioId.ToString()));
    }
}
