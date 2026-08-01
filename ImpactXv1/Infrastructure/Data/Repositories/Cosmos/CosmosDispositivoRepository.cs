using Microsoft.Azure.Cosmos;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Core.Pagination;
using ImpactX.Infrastructure.Data;

namespace ImpactX.Infrastructure.Data.Repositories.Cosmos;

public class CosmosDispositivoRepository : IDispositivoRepository
{
    private readonly Container _container;

    public CosmosDispositivoRepository(CosmosDbContext dbContext)
    {
        _container = dbContext.Dispositivos;
    }

    public async Task<List<Dispositivo>> GetByUsuarioIdAsync(Guid usuarioId)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.usuarioId = @usuarioId")
            .WithParameter("@usuarioId", usuarioId.ToString());

        var results = new List<Dispositivo>();
        using var iterator = _container.GetItemQueryIterator<Dispositivo>(query,
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

    public async Task<List<Dispositivo>> GetActiveByUsuarioIdAsync(Guid usuarioId)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.usuarioId = @usuarioId AND c.activo = true")
            .WithParameter("@usuarioId", usuarioId.ToString());

        var results = new List<Dispositivo>();
        using var iterator = _container.GetItemQueryIterator<Dispositivo>(query,
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

    public async Task<PagedResult<Dispositivo>> GetByUsuarioIdPagedAsync(Guid usuarioId, int pageSize, string? continuationToken, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.usuarioId = @usuarioId ORDER BY c.actualizadoEn DESC")
            .WithParameter("@usuarioId", usuarioId.ToString());

        return await CosmosPageReader.ReadSinglePageAsync<Dispositivo>(
            _container, query, CosmosPartitionKeys.For(usuarioId),
            pageSize, continuationToken, cancellationToken);
    }

    public async Task<Dispositivo?> GetByIdAsync(Guid usuarioId, Guid id)
    {
        try
        {
            var response = await _container.ReadItemAsync<Dispositivo>(
                id.ToString(),
                CosmosPartitionKeys.For(usuarioId));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<Dispositivo?> GetByDeviceIdAsync(Guid usuarioId, string deviceId)
    {
        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.usuarioId = @usuarioId AND c.deviceId = @deviceId")
            .WithParameter("@usuarioId", usuarioId.ToString())
            .WithParameter("@deviceId", deviceId);

        using var iterator = _container.GetItemQueryIterator<Dispositivo>(query,
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

    public async Task<Dispositivo?> GetByTokenFcmAsync(string tokenFcm)
    {
        // Cross-partition justificada (deuda documentada de unicidad global
        // de TokenFcm): la búsqueda es global por diseño. No se registra el
        // token en logs. Detención temprana.
        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.tokenFcm = @tokenFcm")
            .WithParameter("@tokenFcm", tokenFcm);

        var requestOptions = new QueryRequestOptions
        {
            MaxItemCount = 1
        };

        using var iterator = _container.GetItemQueryIterator<Dispositivo>(query, requestOptions: requestOptions);
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }
        return null;
    }

    public async Task AddAsync(Dispositivo dispositivo)
    {
        dispositivo.Id = Guid.NewGuid();
        await _container.CreateItemAsync(dispositivo,
            CosmosPartitionKeys.For(dispositivo.UsuarioId));
    }

    public async Task UpdateAsync(Dispositivo dispositivo)
    {
        await _container.ReplaceItemAsync(dispositivo,
            dispositivo.Id.ToString(),
            CosmosPartitionKeys.For(dispositivo.UsuarioId));
    }

    public async Task DeleteAsync(Dispositivo dispositivo)
    {
        await _container.DeleteItemAsync<Dispositivo>(
            dispositivo.Id.ToString(),
            CosmosPartitionKeys.For(dispositivo.UsuarioId));
    }

    public async Task<int> DeleteAllByUsuarioIdAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        // Proceso completo incremental: pagina, elimina por página y continúa
        // con el token; no acumula todos los dispositivos en memoria.
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.usuarioId = @usuarioId")
            .WithParameter("@usuarioId", usuarioId.ToString());

        var deleted = 0;
        string? continuationToken = null;
        var pk = CosmosPartitionKeys.For(usuarioId);

        do
        {
            var page = await CosmosPageReader.ReadSinglePageAsync<Dispositivo>(
                _container, query, pk, PaginationDefaults.MaxPageSize, continuationToken, cancellationToken);

            foreach (var dispositivo in page.Items)
            {
                await _container.DeleteItemAsync<Dispositivo>(
                    dispositivo.Id.ToString(),
                    CosmosPartitionKeys.For(dispositivo.UsuarioId),
                    cancellationToken: cancellationToken);
                deleted++;
            }

            continuationToken = page.ContinuationToken;
        } while (continuationToken is not null);

        return deleted;
    }
}
