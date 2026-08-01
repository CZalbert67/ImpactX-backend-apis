using Microsoft.Azure.Cosmos;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Infrastructure.Data;

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

        using var iterator = _container.GetItemQueryIterator<Suscripcion>(query,
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

    public async Task<List<Suscripcion>> GetHistoryByUserAsync(Guid usuarioId)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.usuarioId = @usuarioId ORDER BY c.inicio DESC")
            .WithParameter("@usuarioId", usuarioId.ToString());

        var list = new List<Suscripcion>();
        using var iterator = _container.GetItemQueryIterator<Suscripcion>(query,
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

    public async Task<Suscripcion?> GetByIdAsync(Guid id)
    {
        // Cross-partition justificada: el contrato solo recibe el id y
        // Suscripciones particiona por /usuarioId; no hay partition key
        // disponible. Detención temprana.
        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.id = @id")
            .WithParameter("@id", id.ToString());

        using var iterator = _container.GetItemQueryIterator<Suscripcion>(query,
            requestOptions: new QueryRequestOptions { MaxItemCount = 1 });
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }
        return null;
    }

    public async Task AddAsync(Suscripcion suscripcion)
    {
        suscripcion.Id = Guid.NewGuid();
        await _container.CreateItemAsync(suscripcion,
            CosmosPartitionKeys.For(suscripcion.UsuarioId));
    }

    public async Task UpdateAsync(Suscripcion suscripcion)
    {
        await _container.ReplaceItemAsync(suscripcion,
            suscripcion.Id.ToString(),
            CosmosPartitionKeys.For(suscripcion.UsuarioId));
    }

    public async Task<List<Suscripcion>> GetExpiredAsync()
    {
        // Cross-partition justificada: mantenimiento global sin partition key
        // conocida. No usa paginación interna: procesa todas las páginas.
        var now = DateTime.UtcNow.ToString("O");
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE (c.estado = 'Activa' OR c.estado = 'Trial') AND c.fin != null AND c.fin <= @now")
            .WithParameter("@now", now);

        var list = new List<Suscripcion>();
        using var iterator = _container.GetItemQueryIterator<Suscripcion>(query,
            requestOptions: new QueryRequestOptions { MaxItemCount = 100 });
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            list.AddRange(response);
        }
        return list;
    }

    public async Task<List<Suscripcion>> GetTrialsEndingAsync(int daysRemaining)
    {
        // Cross-partition justificada: mantenimiento global sin partition key
        // conocida. No usa paginación interna: procesa todas las páginas.
        var threshold = DateTime.UtcNow.AddDays(daysRemaining).ToString("O");
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.estado = 'Trial' AND c.trialFin != null AND c.trialFin <= @threshold")
            .WithParameter("@threshold", threshold);

        var list = new List<Suscripcion>();
        using var iterator = _container.GetItemQueryIterator<Suscripcion>(query,
            requestOptions: new QueryRequestOptions { MaxItemCount = 100 });
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            list.AddRange(response);
        }
        return list;
    }
}
