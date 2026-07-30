using Microsoft.Azure.Cosmos;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;

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
        using var iterator = _container.GetItemQueryIterator<Notificacion>(query);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }
        return results;
    }

    public async Task<Notificacion?> GetByIdAsync(Guid id)
    {
        try
        {
            var response = await _container.ReadItemAsync<Notificacion>(
                id.ToString(), new PartitionKey(id.ToString()));
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

        using var iterator = _container.GetItemQueryIterator<int>(query);
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
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.claveIdempotencia = @key")
                .WithParameter("@key", key);

            using var iterator = _container.GetItemQueryIterator<Notificacion>(query);
            if (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                return response.FirstOrDefault();
            }
            return null;
        }

        var pk = new PartitionKey(recipientUserId.Value.ToString());
        var queryWithPk = new QueryDefinition(
            "SELECT * FROM c WHERE c.claveIdempotencia = @key AND c.usuarioId = @uid")
            .WithParameter("@key", key)
            .WithParameter("@uid", recipientUserId.Value.ToString());

        using var iteratorWithPk = _container.GetItemQueryIterator<Notificacion>(queryWithPk, requestOptions: new QueryRequestOptions { PartitionKey = pk });
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
        await _container.CreateItemAsync(notificacion, new PartitionKey(notificacion.UsuarioId.ToString()));
    }

    public async Task UpdateAsync(Notificacion notificacion)
    {
        await _container.UpsertItemAsync(notificacion, new PartitionKey(notificacion.UsuarioId.ToString()));
    }

    public async Task MarkAllAsReadAsync(Guid usuarioId)
    {
        var notificaciones = await GetByUserAsync(usuarioId);
        var pendientes = notificaciones.Where(n => !n.Leida).ToList();

        foreach (var n in pendientes)
        {
            n.Leida = true;
            n.LeidaEn = DateTime.UtcNow;
            await UpdateAsync(n);
        }
    }

    public async Task DeleteAsync(Notificacion notificacion)
    {
        await _container.DeleteItemAsync<Notificacion>(
            notificacion.Id.ToString(),
            new PartitionKey(notificacion.UsuarioId.ToString()));
    }

    public async Task DeleteAllByUserAsync(Guid usuarioId)
    {
        var notificaciones = await GetByUserAsync(usuarioId);
        foreach (var n in notificaciones)
        {
            await DeleteAsync(n);
        }
    }
}
