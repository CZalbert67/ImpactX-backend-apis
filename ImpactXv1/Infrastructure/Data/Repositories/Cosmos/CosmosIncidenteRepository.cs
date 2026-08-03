using Microsoft.Azure.Cosmos;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Infrastructure.Data;

namespace ImpactX.Infrastructure.Data.Repositories.Cosmos;

public class CosmosIncidenteRepository : IIncidenteRepository
{
    private readonly Container _container;

    public CosmosIncidenteRepository(CosmosDbContext dbContext)
    {
        _container = dbContext.Incidentes;
    }

    public async Task<Incidente?> GetByIdAsync(Guid id)
    {
        // Cross-partition justificada: el contrato solo recibe el id e
        // Incidentes particiona por /usuarioId. Los servicios que conocen el
        // usuario deben usar GetByIdAsync(usuarioId, id) (point-read).
        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.id = @id")
            .WithParameter("@id", id.ToString());

        using var iterator = _container.GetItemQueryIterator<Incidente>(query,
            requestOptions: new QueryRequestOptions { MaxItemCount = 1 });
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }
        return null;
    }

    public async Task<Incidente?> GetByIdAsync(Guid usuarioId, Guid id)
    {
        try
        {
            var response = await _container.ReadItemAsync<Incidente>(
                id.ToString(),
                CosmosPartitionKeys.For(usuarioId));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<Incidente?> GetByAlertIdAsync(Guid usuarioId, Guid alertaId)
    {
        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.usuarioId = @usuarioId AND c.alertaId = @alertaId")
            .WithParameter("@usuarioId", usuarioId.ToString())
            .WithParameter("@alertaId", alertaId.ToString());

        using var iterator = _container.GetItemQueryIterator<Incidente>(query,
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

    public async Task<List<Incidente>> GetByUserAsync(Guid usuarioId)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.usuarioId = @usuarioId ORDER BY c.creadoEn DESC")
            .WithParameter("@usuarioId", usuarioId.ToString());

        var results = new List<Incidente>();
        using var iterator = _container.GetItemQueryIterator<Incidente>(query,
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

    public async Task<List<Incidente>> GetFilteredAsync(Guid usuarioId, string? severidad, DateTime? desde, DateTime? hasta, int pagina, int tamano)
    {
        var spec = new IncidenteQueryBuilder.IncidenteFilterSpec(usuarioId, severidad, desde, hasta);
        var where = IncidenteQueryBuilder.BuildWhereClause(spec);
        var offset = (pagina - 1) * tamano;
        var queryText = $"SELECT * FROM c WHERE {where} ORDER BY c.creadoEn DESC OFFSET {offset} LIMIT {tamano}";

        var query = new QueryDefinition(queryText);
        IncidenteQueryBuilder.AddFilterParameters(query, spec);

        var results = new List<Incidente>();
        using var iterator = _container.GetItemQueryIterator<Incidente>(query,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = CosmosPartitionKeys.For(usuarioId),
                MaxItemCount = Math.Max(1, tamano)
            });
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }
        return results;
    }

    public async Task<int> CountFilteredAsync(Guid usuarioId, string? severidad, DateTime? desde, DateTime? hasta)
    {
        var spec = new IncidenteQueryBuilder.IncidenteFilterSpec(usuarioId, severidad, desde, hasta);
        var where = IncidenteQueryBuilder.BuildWhereClause(spec);
        var query = new QueryDefinition($"SELECT VALUE COUNT(1) FROM c WHERE {where}");
        IncidenteQueryBuilder.AddFilterParameters(query, spec);

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

    public async Task AddAsync(Incidente incidente)
    {
        incidente.Id = Guid.NewGuid();
        await _container.CreateItemAsync(incidente,
            CosmosPartitionKeys.For(incidente.UsuarioId));
    }

    public async Task UpdateAsync(Incidente incidente)
    {
        await _container.ReplaceItemAsync(incidente,
            incidente.Id.ToString(),
            CosmosPartitionKeys.For(incidente.UsuarioId));
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
