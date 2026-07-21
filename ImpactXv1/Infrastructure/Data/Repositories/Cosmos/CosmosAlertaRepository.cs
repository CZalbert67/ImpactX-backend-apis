using Microsoft.Azure.Cosmos;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;

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
        try
        {
            var response = await _container.ReadItemAsync<Alerta>(
                id.ToString(), new PartitionKey(id.ToString()));
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
        using var iterator = _container.GetItemQueryIterator<Alerta>(query);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }
        return results;
    }

    public async Task<Alerta?> GetActiveByUserAsync(Guid usuarioId)
    {
        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.usuarioId = @usuarioId AND c.estado NOT IN ('Cerrada', 'Atendida', 'FalsaAlarma') ORDER BY c.creadoEn DESC")
            .WithParameter("@usuarioId", usuarioId.ToString());

        using var iterator = _container.GetItemQueryIterator<Alerta>(query);
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }
        return null;
    }

    public async Task<List<Alerta>> GetPendingByUserAsync(Guid usuarioId)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.usuarioId = @usuarioId AND c.estado = 'Pendiente' ORDER BY c.creadoEn DESC")
            .WithParameter("@usuarioId", usuarioId.ToString());

        var results = new List<Alerta>();
        using var iterator = _container.GetItemQueryIterator<Alerta>(query);
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
        using var iterator = _container.GetItemQueryIterator<Alerta>(query);
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
        await _container.CreateItemAsync(alerta, new PartitionKey(alerta.UsuarioId.ToString()));
    }

    public async Task UpdateAsync(Alerta alerta)
    {
        await _container.UpsertItemAsync(alerta, new PartitionKey(alerta.UsuarioId.ToString()));
    }

    public async Task<int> CountByUserAsync(Guid usuarioId)
    {
        var query = new QueryDefinition(
            "SELECT VALUE COUNT(1) FROM c WHERE c.usuarioId = @usuarioId")
            .WithParameter("@usuarioId", usuarioId.ToString());

        using var iterator = _container.GetItemQueryIterator<int>(query);
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }
        return 0;
    }
}
