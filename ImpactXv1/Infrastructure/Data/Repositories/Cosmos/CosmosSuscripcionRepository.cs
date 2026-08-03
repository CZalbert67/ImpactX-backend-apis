using Microsoft.Azure.Cosmos;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Core.Pagination;
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

    public async Task<Suscripcion?> GetCurrentByUserAsync(Guid usuarioId)
    {
        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.usuarioId = @usuarioId AND (c.estado = 'Trial' OR c.estado = 'Activa' OR c.estado = 'Grace') ORDER BY c.inicio DESC")
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

    public async Task<PagedResult<Suscripcion>> GetHistoryByUserPagedAsync(Guid usuarioId, int pageSize, string? continuationToken, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.usuarioId = @usuarioId ORDER BY c.inicio DESC")
            .WithParameter("@usuarioId", usuarioId.ToString());

        return await CosmosPageReader.ReadSinglePageAsync<Suscripcion>(
            _container, query, CosmosPartitionKeys.For(usuarioId),
            pageSize, continuationToken, cancellationToken);
    }

    public async Task<Suscripcion?> GetByIdAsync(Guid id)
    {
        // Cross-partition justificada: el contrato solo recibe el id y
        // Suscripciones particiona por /usuarioId. Los servicios que conocen
        // el usuario deben usar GetByIdAsync(usuarioId, id) (point-read).
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

    public async Task<Suscripcion?> GetByIdAsync(Guid usuarioId, Guid id)
    {
        try
        {
            var response = await _container.ReadItemAsync<Suscripcion>(
                id.ToString(),
                CosmosPartitionKeys.For(usuarioId));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
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
        // conocida. Proceso completo: el consumidor que solo necesita
        // recorrer debe usar ExpireAllAsync.
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
        // conocida. Proceso completo: el consumidor que solo necesita
        // recorrer debe usar ProcessTrialsEndingAsync.
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

    public async Task<int> ExpireAllAsync(Func<Suscripcion, CancellationToken, Task> process, CancellationToken cancellationToken = default)
    {
        // Proceso completo incremental: página por página, sin acumular todo
        // el conjunto antes de procesar. Cross-partition justificada
        // (mantenimiento global).
        var now = DateTime.UtcNow.ToString("O");
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE (c.estado = 'Activa' OR c.estado = 'Trial') AND c.fin != null AND c.fin <= @now")
            .WithParameter("@now", now);

        var processed = 0;
        string? continuationToken = null;

        do
        {
            var page = await CosmosPageReader.ReadSinglePageAsync<Suscripcion>(
                _container, query, null, PaginationDefaults.MaxPageSize, continuationToken, cancellationToken);

            foreach (var s in page.Items)
            {
                await process(s, cancellationToken);
                processed++;
            }

            continuationToken = page.ContinuationToken;
        } while (continuationToken is not null);

        return processed;
    }

    public async Task<int> ProcessLifecycleAsync(
        DateTime utcNow,
        Func<Suscripcion, CancellationToken, Task> process,
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE (((c.estado = 'Activa' OR c.estado = 'Trial') AND c.fin != null AND c.fin <= @now) OR (c.estado = 'Grace' AND c.graceEndsAtUtc != null AND c.graceEndsAtUtc <= @now))")
            .WithParameter("@now", utcNow.ToString("O"));

        var processed = 0;
        string? continuationToken = null;
        do
        {
            var page = await CosmosPageReader.ReadSinglePageAsync<Suscripcion>(
                _container, query, null, PaginationDefaults.MaxPageSize, continuationToken, cancellationToken);
            foreach (var subscription in page.Items)
            {
                await process(subscription, cancellationToken);
                processed++;
            }
            continuationToken = page.ContinuationToken;
        } while (continuationToken is not null);

        return processed;
    }

    public async Task<int> ProcessTrialsEndingAsync(int daysRemaining, Func<Suscripcion, CancellationToken, Task> process, CancellationToken cancellationToken = default)
    {
        // Proceso completo incremental: página por página, sin acumular todo
        // el conjunto antes de procesar. Cross-partition justificada
        // (mantenimiento global).
        var threshold = DateTime.UtcNow.AddDays(daysRemaining).ToString("O");
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.estado = 'Trial' AND c.trialFin != null AND c.trialFin <= @threshold")
            .WithParameter("@threshold", threshold);

        var processed = 0;
        string? continuationToken = null;

        do
        {
            var page = await CosmosPageReader.ReadSinglePageAsync<Suscripcion>(
                _container, query, null, PaginationDefaults.MaxPageSize, continuationToken, cancellationToken);

            foreach (var s in page.Items)
            {
                await process(s, cancellationToken);
                processed++;
            }

            continuationToken = page.ContinuationToken;
        } while (continuationToken is not null);

        return processed;
    }
}
