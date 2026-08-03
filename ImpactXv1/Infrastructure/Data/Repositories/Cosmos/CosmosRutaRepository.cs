using Microsoft.Azure.Cosmos;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Core.Pagination;
using ImpactX.Infrastructure.Data;

namespace ImpactX.Infrastructure.Data.Repositories.Cosmos;

public class CosmosRutaRepository : IRutaRepository
{
    private readonly Container _container;

    public CosmosRutaRepository(CosmosDbContext dbContext)
    {
        _container = dbContext.Rutas;
    }

    public async Task<List<Ruta>> GetByUserAsync(Guid usuarioId)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.usuarioId = @usuarioId ORDER BY c.creadoEn DESC")
            .WithParameter("@usuarioId", usuarioId.ToString());

        var results = new List<Ruta>();
        using var iterator = _container.GetItemQueryIterator<Ruta>(query,
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

    public async Task<List<Ruta>> GetFrequentByUserAsync(Guid usuarioId)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.usuarioId = @usuarioId AND c.esFrecuente = true ORDER BY c.usadaEn DESC")
            .WithParameter("@usuarioId", usuarioId.ToString());

        var results = new List<Ruta>();
        using var iterator = _container.GetItemQueryIterator<Ruta>(query,
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

    public async Task<List<Ruta>> GetHistoryByUserAsync(Guid usuarioId)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.usuarioId = @usuarioId AND c.esFrecuente = false ORDER BY c.creadoEn DESC")
            .WithParameter("@usuarioId", usuarioId.ToString());

        var results = new List<Ruta>();
        using var iterator = _container.GetItemQueryIterator<Ruta>(query,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = CosmosPartitionKeys.For(usuarioId),
                MaxItemCount = 50
            });
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
            if (results.Count >= 50) break;
        }
        return results;
    }

    public async Task<PagedResult<Ruta>> GetByUserPagedAsync(Guid usuarioId, int pageSize, string? continuationToken, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.usuarioId = @usuarioId ORDER BY c.creadoEn DESC")
            .WithParameter("@usuarioId", usuarioId.ToString());

        return await CosmosPageReader.ReadSinglePageAsync<Ruta>(
            _container, query, CosmosPartitionKeys.For(usuarioId),
            pageSize, continuationToken, cancellationToken);
    }

    public async Task<PagedResult<Ruta>> GetFrequentByUserPagedAsync(Guid usuarioId, int pageSize, string? continuationToken, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.usuarioId = @usuarioId AND c.esFrecuente = true ORDER BY c.usadaEn DESC")
            .WithParameter("@usuarioId", usuarioId.ToString());

        return await CosmosPageReader.ReadSinglePageAsync<Ruta>(
            _container, query, CosmosPartitionKeys.For(usuarioId),
            pageSize, continuationToken, cancellationToken);
    }

    public async Task<PagedResult<Ruta>> GetHistoryByUserPagedAsync(Guid usuarioId, int pageSize, string? continuationToken, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.usuarioId = @usuarioId AND c.esFrecuente = false ORDER BY c.creadoEn DESC")
            .WithParameter("@usuarioId", usuarioId.ToString());

        return await CosmosPageReader.ReadSinglePageAsync<Ruta>(
            _container, query, CosmosPartitionKeys.For(usuarioId),
            pageSize, continuationToken, cancellationToken);
    }

    public async Task<Ruta?> GetByIdAsync(Guid id)
    {
        // Cross-partition justificada: el contrato solo recibe el id y Rutas
        // particiona por /usuarioId. Los servicios que conocen el usuario
        // deben usar GetByIdAsync(usuarioId, id) (point-read).
        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.id = @id")
            .WithParameter("@id", id.ToString());

        using var iterator = _container.GetItemQueryIterator<Ruta>(query,
            requestOptions: new QueryRequestOptions { MaxItemCount = 1 });
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }
        return null;
    }

    public async Task<Ruta?> GetByIdAsync(Guid usuarioId, Guid id)
    {
        try
        {
            var response = await _container.ReadItemAsync<Ruta>(
                id.ToString(),
                CosmosPartitionKeys.For(usuarioId));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<Ruta?> GetSelectedTodayAsync(Guid usuarioId)
    {
        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.usuarioId = @usuarioId AND c.seleccionadaHoy = true")
            .WithParameter("@usuarioId", usuarioId.ToString());

        using var iterator = _container.GetItemQueryIterator<Ruta>(query,
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

    public async Task AddAsync(Ruta ruta)
    {
        ruta.Id = Guid.NewGuid();
        await _container.CreateItemAsync(ruta,
            CosmosPartitionKeys.For(ruta.UsuarioId));
    }

    public async Task UpdateAsync(Ruta ruta)
    {
        await _container.ReplaceItemAsync(ruta,
            ruta.Id.ToString(),
            CosmosPartitionKeys.For(ruta.UsuarioId));
    }

    public async Task DeleteAsync(Ruta ruta)
    {
        await _container.DeleteItemAsync<Ruta>(
            ruta.Id.ToString(),
            CosmosPartitionKeys.For(ruta.UsuarioId));
    }
}
