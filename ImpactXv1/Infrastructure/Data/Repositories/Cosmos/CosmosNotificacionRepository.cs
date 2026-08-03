using Microsoft.Azure.Cosmos;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Core.Pagination;
using ImpactX.Infrastructure.Data;

namespace ImpactX.Infrastructure.Data.Repositories.Cosmos;

public class CosmosNotificacionRepository : INotificacionRepository
{
    private readonly Container _container;

    public CosmosNotificacionRepository(CosmosDbContext dbContext)
    {
        _container = dbContext.Notificaciones;
    }

    public async Task<List<Notificacion>> GetByUserAsync(Guid usuarioId)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.usuarioId = @usuarioId ORDER BY c.creadoEn DESC")
            .WithParameter("@usuarioId", usuarioId.ToString());

        var results = new List<Notificacion>();
        using var iterator = _container.GetItemQueryIterator<Notificacion>(query,
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

    public async Task<PagedResult<Notificacion>> GetByUserPagedAsync(Guid usuarioId, int pageSize, string? continuationToken, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.usuarioId = @usuarioId ORDER BY c.creadoEn DESC")
            .WithParameter("@usuarioId", usuarioId.ToString());

        return await CosmosPageReader.ReadSinglePageAsync<Notificacion>(
            _container, query, CosmosPartitionKeys.For(usuarioId),
            pageSize, continuationToken, cancellationToken);
    }

    public async Task<Notificacion?> GetByIdAsync(Guid id)
    {
        // Cross-partition justificada: el contrato solo recibe el id y
        // Notificaciones particiona por /usuarioId. Los servicios que conocen
        // el usuario deben usar GetByIdAsync(usuarioId, id) (point-read).
        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.id = @id")
            .WithParameter("@id", id.ToString());

        using var iterator = _container.GetItemQueryIterator<Notificacion>(query,
            requestOptions: new QueryRequestOptions { MaxItemCount = 1 });
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }
        return null;
    }

    public async Task<Notificacion?> GetByIdAsync(Guid usuarioId, Guid id)
    {
        try
        {
            var response = await _container.ReadItemAsync<Notificacion>(
                id.ToString(),
                CosmosPartitionKeys.For(usuarioId));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<int> CountUnreadByUserAsync(Guid usuarioId)
    {
        var query = new QueryDefinition(
            "SELECT VALUE COUNT(1) FROM c WHERE c.usuarioId = @usuarioId AND c.leida = false")
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

    public async Task<Notificacion?> GetByIdempotencyKeyAsync(string key, Guid? recipientUserId = null, CancellationToken cancellationToken = default)
    {
        if (recipientUserId == null)
        {
            // Cross-partition justificada: idempotencia sin contexto de
            // usuario; no hay partition key disponible.
            var query = new QueryDefinition(
                "SELECT TOP 1 * FROM c WHERE c.claveIdempotencia = @key")
                .WithParameter("@key", key);

            using var iterator = _container.GetItemQueryIterator<Notificacion>(query,
                requestOptions: new QueryRequestOptions { MaxItemCount = 1 });
            if (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                return response.FirstOrDefault();
            }
            return null;
        }

        var pk = CosmosPartitionKeys.For(recipientUserId.Value);
        var queryWithPk = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.claveIdempotencia = @key AND c.usuarioId = @uid")
            .WithParameter("@key", key)
            .WithParameter("@uid", recipientUserId.Value.ToString());

        using var iteratorWithPk = _container.GetItemQueryIterator<Notificacion>(queryWithPk,
            requestOptions: new QueryRequestOptions { PartitionKey = pk, MaxItemCount = 1 });
        if (iteratorWithPk.HasMoreResults)
        {
            var response = await iteratorWithPk.ReadNextAsync(cancellationToken);
            return response.FirstOrDefault();
        }
        return null;
    }

    public async Task AddAsync(Notificacion notificacion)
    {
        notificacion.Id = Guid.NewGuid();
        await _container.CreateItemAsync(notificacion,
            CosmosPartitionKeys.For(notificacion.UsuarioId));
    }

    public async Task UpdateAsync(Notificacion notificacion)
    {
        await _container.ReplaceItemAsync(notificacion,
            notificacion.Id.ToString(),
            CosmosPartitionKeys.For(notificacion.UsuarioId));
    }

    public async Task MarkAllAsReadAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        // Proceso completo: pagina sobre las no leídas, actualiza cada página
        // y continúa con el token; no acumula todas en memoria.
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.usuarioId = @usuarioId AND c.leida = false ORDER BY c.creadoEn DESC")
            .WithParameter("@usuarioId", usuarioId.ToString());

        string? continuationToken = null;
        var pk = CosmosPartitionKeys.For(usuarioId);

        do
        {
            var page = await CosmosPageReader.ReadSinglePageAsync<Notificacion>(
                _container, query, pk, PaginationDefaults.MaxPageSize, continuationToken, cancellationToken);

            var now = DateTime.UtcNow;
            foreach (var n in page.Items)
            {
                n.Leida = true;
                n.LeidaEn = now;
                await UpdateAsync(n);
            }

            continuationToken = page.ContinuationToken;
        } while (continuationToken is not null);
    }

    public async Task DeleteAsync(Notificacion notificacion)
    {
        await _container.DeleteItemAsync<Notificacion>(
            notificacion.Id.ToString(),
            CosmosPartitionKeys.For(notificacion.UsuarioId));
    }

    public async Task DeleteAllByUserAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        // Proceso completo: pagina, elimina por página y continúa; no acumula
        // todas las notificaciones en memoria.
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.usuarioId = @usuarioId ORDER BY c.creadoEn DESC")
            .WithParameter("@usuarioId", usuarioId.ToString());

        string? continuationToken = null;
        var pk = CosmosPartitionKeys.For(usuarioId);

        do
        {
            var page = await CosmosPageReader.ReadSinglePageAsync<Notificacion>(
                _container, query, pk, PaginationDefaults.MaxPageSize, continuationToken, cancellationToken);

            foreach (var n in page.Items)
            {
                await DeleteAsync(n);
            }

            continuationToken = page.ContinuationToken;
        } while (continuationToken is not null);
    }
}
