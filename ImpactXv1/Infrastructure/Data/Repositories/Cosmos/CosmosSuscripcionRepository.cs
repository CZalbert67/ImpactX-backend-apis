using Microsoft.Azure.Cosmos;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;

namespace ImpactX.Infrastructure.Data.Repositories.Cosmos;

public class CosmosSuscripcionRepository : ISuscripcionRepository
{
    private readonly Container _container;

    public CosmosSuscripcionRepository(CosmosDbContext dbContext)
    {
        _container = dbContext.Suscripciones;
    }

    public async Task<Suscripcion?> GetActiveByUserAsync(Guid usuarioId)
    {
        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.usuarioId = @usuarioId AND (c.estado = 'Trial' OR c.estado = 'Activa') ORDER BY c.inicio DESC")
            .WithParameter("@usuarioId", usuarioId.ToString());

        using var iterator = _container.GetItemQueryIterator<Suscripcion>(query);
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }
        return null;
    }

    public async Task<List<Suscripcion>> GetHistoryByUserAsync(Guid usuarioId)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.usuarioId = @usuarioId ORDER BY c.inicio DESC")
            .WithParameter("@usuarioId", usuarioId.ToString());

        var list = new List<Suscripcion>();
        using var iterator = _container.GetItemQueryIterator<Suscripcion>(query);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            list.AddRange(response);
        }
        return list;
    }

    public async Task<Suscripcion?> GetByIdAsync(Guid id)
    {
        try
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @id")
                .WithParameter("@id", id.ToString());
            using var iterator = _container.GetItemQueryIterator<Suscripcion>(query);
            if (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                return response.FirstOrDefault();
            }
            return null;
        }
        catch (CosmosException) { return null; }
    }

    public async Task AddAsync(Suscripcion suscripcion)
    {
        suscripcion.Id = Guid.NewGuid();
        await _container.CreateItemAsync(suscripcion,
            new PartitionKey(suscripcion.UsuarioId.ToString()));
    }

    public async Task UpdateAsync(Suscripcion suscripcion)
    {
        await _container.UpsertItemAsync(suscripcion,
            new PartitionKey(suscripcion.UsuarioId.ToString()));
    }

    public async Task<List<Suscripcion>> GetExpiredAsync()
    {
        var now = DateTime.UtcNow.ToString("O");
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE (c.estado = 'Activa' OR c.estado = 'Trial') AND c.fin != null AND c.fin <= @now")
            .WithParameter("@now", now);

        var list = new List<Suscripcion>();
        using var iterator = _container.GetItemQueryIterator<Suscripcion>(query);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            list.AddRange(response);
        }
        return list;
    }

    public async Task<List<Suscripcion>> GetTrialsEndingAsync(int daysRemaining)
    {
        var threshold = DateTime.UtcNow.AddDays(daysRemaining).ToString("O");
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.estado = 'Trial' AND c.trialFin != null AND c.trialFin <= @threshold")
            .WithParameter("@threshold", threshold);

        var list = new List<Suscripcion>();
        using var iterator = _container.GetItemQueryIterator<Suscripcion>(query);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            list.AddRange(response);
        }
        return list;
    }
}
