using Microsoft.Azure.Cosmos;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Core.Pagination;
using ImpactX.Infrastructure.Data;

namespace ImpactX.Infrastructure.Data.Repositories.Cosmos;

public class CosmosAlertaRepository : IAlertaRepository
{
    private readonly Container _container;

    public CosmosAlertaRepository(CosmosDbContext dbContext)
    {
        _container = dbContext.Alertas;
    }

    public async Task<Alerta?> GetByIdAsync(Guid id)
    {
        // Cross-partition justificada: el contrato solo recibe el id y
        // Alertas particiona por /usuarioId. Los servicios que conocen el
        // usuario deben usar GetByIdAsync(usuarioId, id) (point-read).
        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.id = @id")
            .WithParameter("@id", id.ToString());

        using var iterator = _container.GetItemQueryIterator<Alerta>(query,
            requestOptions: new QueryRequestOptions { MaxItemCount = 1 });
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }
        return null;
    }

    public async Task<Alerta?> GetByIdAsync(Guid usuarioId, Guid id)
    {
        try
        {
            var response = await _container.ReadItemAsync<Alerta>(
                id.ToString(),
                CosmosPartitionKeys.For(usuarioId));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<List<Alerta>> GetByUserAsync(Guid usuarioId)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.usuarioId = @usuarioId ORDER BY c.creadoEn DESC")
            .WithParameter("@usuarioId", usuarioId.ToString());

        var results = new List<Alerta>();
        using var iterator = _container.GetItemQueryIterator<Alerta>(query,
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

    public async Task<PagedResult<Alerta>> GetByUserPagedAsync(Guid usuarioId, int pageSize, string? continuationToken, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.usuarioId = @usuarioId ORDER BY c.creadoEn DESC")
            .WithParameter("@usuarioId", usuarioId.ToString());

        return await CosmosPageReader.ReadSinglePageAsync<Alerta>(
            _container, query, CosmosPartitionKeys.For(usuarioId),
            pageSize, continuationToken, cancellationToken);
    }

    public async Task<Alerta?> GetActiveByUserAsync(Guid usuarioId)
    {
        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.usuarioId = @usuarioId AND c.estado NOT IN ('Cerrada', 'Atendida', 'FalsaAlarma') ORDER BY c.creadoEn DESC")
            .WithParameter("@usuarioId", usuarioId.ToString());

        using var iterator = _container.GetItemQueryIterator<Alerta>(query,
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

    public async Task<Alerta?> GetBySourceTelemetryEventIdAsync(
        Guid usuarioId,
        Guid sourceTelemetryEventId,
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.usuarioId = @usuarioId AND c.sourceTelemetryEventId = @eventId")
            .WithParameter("@usuarioId", usuarioId.ToString())
            .WithParameter("@eventId", sourceTelemetryEventId.ToString());

        using var iterator = _container.GetItemQueryIterator<Alerta>(
            query,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = CosmosPartitionKeys.For(usuarioId),
                MaxItemCount = 1
            });

        if (!iterator.HasMoreResults)
            return null;

        var response = await iterator.ReadNextAsync(cancellationToken);
        return response.FirstOrDefault();
    }

    public async Task<IReadOnlyList<Alerta>> GetPendingDueAsync(
        DateTime utcNow,
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        // Consulta cross-partition deliberada: el worker procesa alertas
        // vencidas de todos los usuarios. Se limita estrictamente el lote.
        var take = Math.Clamp(maxCount, 1, 500);
        var query = new QueryDefinition(
            $"SELECT TOP {take} * FROM c WHERE c.estado = 'Pendiente' " +
            "AND IS_DEFINED(c.autoSendAtUtc) AND c.autoSendAtUtc <= @utcNow " +
            "ORDER BY c.autoSendAtUtc ASC")
            .WithParameter("@utcNow", utcNow);

        var results = new List<Alerta>();
        using var iterator = _container.GetItemQueryIterator<Alerta>(
            query,
            requestOptions: new QueryRequestOptions { MaxItemCount = take });

        while (iterator.HasMoreResults && results.Count < take)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response);
        }

        return results.Take(take).ToList();
    }

    public async Task<List<Alerta>> GetPendingByUserAsync(Guid usuarioId)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.usuarioId = @usuarioId AND c.estado = 'Pendiente' ORDER BY c.creadoEn DESC")
            .WithParameter("@usuarioId", usuarioId.ToString());

        var results = new List<Alerta>();
        using var iterator = _container.GetItemQueryIterator<Alerta>(query,
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

    public async Task<List<Alerta>> GetActiveAlertsAsync(Guid usuarioId)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.usuarioId = @usuarioId AND (c.estado = 'Pendiente' OR c.estado = 'Enviada' OR c.estado = 'Activa') ORDER BY c.creadoEn DESC")
            .WithParameter("@usuarioId", usuarioId.ToString());

        var results = new List<Alerta>();
        using var iterator = _container.GetItemQueryIterator<Alerta>(query,
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

    public async Task AddAsync(Alerta alerta)
    {
        alerta.Id = Guid.NewGuid();
        await _container.CreateItemAsync(alerta,
            CosmosPartitionKeys.For(alerta.UsuarioId));
    }

    public async Task UpdateAsync(Alerta alerta)
    {
        await _container.ReplaceItemAsync(alerta,
            alerta.Id.ToString(),
            CosmosPartitionKeys.For(alerta.UsuarioId));
    }

    public async Task<int> CountByUserAsync(Guid usuarioId)
    {
        var query = new QueryDefinition(
            "SELECT VALUE COUNT(1) FROM c WHERE c.usuarioId = @usuarioId")
            .WithParameter("@usuarioId", usuarioId.ToString());

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
}
