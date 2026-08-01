using Microsoft.Azure.Cosmos;
using Monitor = ImpactX.Core.Domain.Monitor;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Infrastructure.Data;

namespace ImpactX.Infrastructure.Data.Repositories.Cosmos;

public class CosmosMonitorRepository : IMonitorRepository
{
    private readonly Container _container;

    public CosmosMonitorRepository(CosmosDbContext dbContext)
    {
        _container = dbContext.Monitores;
    }

    public async Task<List<Monitor>> GetByUserAsync(Guid usuarioId)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.usuarioId = @usuarioId ORDER BY c.creadoEn DESC")
            .WithParameter("@usuarioId", usuarioId.ToString());

        var results = new List<Monitor>();
        using var iterator = _container.GetItemQueryIterator<Monitor>(query,
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

    public async Task<Monitor?> GetByIdAsync(Guid id)
    {
        // Cross-partition justificada: el contrato solo recibe el id y
        // Monitores particiona por /usuarioId. Corrige el ReadItemAsync
        // anterior con partition key incorrecta que siempre devolvía 404.
        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.id = @id")
            .WithParameter("@id", id.ToString());

        using var iterator = _container.GetItemQueryIterator<Monitor>(query,
            requestOptions: new QueryRequestOptions { MaxItemCount = 1 });
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }
        return null;
    }

    public async Task<List<Monitor>> GetActiveByUserAsync(Guid usuarioId)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.usuarioId = @usuarioId AND c.estado = 'Activo'")
            .WithParameter("@usuarioId", usuarioId.ToString());

        var results = new List<Monitor>();
        using var iterator = _container.GetItemQueryIterator<Monitor>(query,
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

    public async Task<int> CountActiveByUserAsync(Guid usuarioId)
    {
        var query = new QueryDefinition(
            "SELECT VALUE COUNT(1) FROM c WHERE c.usuarioId = @usuarioId AND c.estado = 'Activo'")
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

    public async Task<Monitor?> GetByTokenAsync(string token)
    {
        // Cross-partition justificada: búsqueda por token de invitación sin
        // usuarioId conocido; la partición es /usuarioId. Detención temprana.
        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.tokenInvitacion = @token")
            .WithParameter("@token", token);

        using var iterator = _container.GetItemQueryIterator<Monitor>(query,
            requestOptions: new QueryRequestOptions { MaxItemCount = 1 });
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }
        return null;
    }

    public async Task<Monitor?> GetByUsuarioYMonitorAsync(Guid usuarioId, Guid monitorUsuarioId)
    {
        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.usuarioId = @usuarioId AND c.profileId = @monitorId")
            .WithParameter("@usuarioId", usuarioId.ToString())
            .WithParameter("@monitorId", monitorUsuarioId.ToString());

        using var iterator = _container.GetItemQueryIterator<Monitor>(query,
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

    public async Task<bool> ExistsByUsernameAsync(Guid usuarioId, string username)
    {
        var query = new QueryDefinition(
            "SELECT VALUE COUNT(1) FROM c WHERE c.usuarioId = @usuarioId AND c.username = @username AND c.estado != 'Revocado'")
            .WithParameter("@usuarioId", usuarioId.ToString())
            .WithParameter("@username", username);

        using var iterator = _container.GetItemQueryIterator<int>(query,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = CosmosPartitionKeys.For(usuarioId),
                MaxItemCount = 1
            });
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault() > 0;
        }
        return false;
    }

    public async Task AddAsync(Monitor monitor)
    {
        monitor.Id = Guid.NewGuid();
        await _container.CreateItemAsync(monitor,
            CosmosPartitionKeys.For(monitor.UsuarioId));
    }

    public async Task UpdateAsync(Monitor monitor)
    {
        await _container.ReplaceItemAsync(monitor,
            monitor.Id.ToString(),
            CosmosPartitionKeys.For(monitor.UsuarioId));
    }

    public async Task DeleteAsync(Monitor monitor)
    {
        await _container.DeleteItemAsync<Monitor>(
            monitor.Id.ToString(),
            CosmosPartitionKeys.For(monitor.UsuarioId));
    }
}
