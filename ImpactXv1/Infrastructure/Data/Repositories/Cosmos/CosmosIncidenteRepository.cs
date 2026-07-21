using Microsoft.Azure.Cosmos;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;

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
        try
        {
            var response = await _container.ReadItemAsync<Incidente>(
                id.ToString(), new PartitionKey(id.ToString()));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<List<Incidente>> GetByUserAsync(Guid usuarioId)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.usuarioId = @usuarioId ORDER BY c.creadoEn DESC")
            .WithParameter("@usuarioId", usuarioId.ToString());

        var results = new List<Incidente>();
        using var iterator = _container.GetItemQueryIterator<Incidente>(query);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }
        return results;
    }

    public async Task<List<Incidente>> GetFilteredAsync(Guid usuarioId, string? severidad, DateTime? desde, DateTime? hasta, int pagina, int tamano)
    {
        var whereClauses = new List<string> { "c.usuarioId = @usuarioId" };

        if (!string.IsNullOrWhiteSpace(severidad))
            whereClauses.Add("c.severidad = @severidad");

        if (desde.HasValue)
            whereClauses.Add("c.creadoEn >= @desde");

        if (hasta.HasValue)
            whereClauses.Add("c.creadoEn <= @hasta");

        var where = string.Join(" AND ", whereClauses);
        var queryText = $"SELECT * FROM c WHERE {where} ORDER BY c.creadoEn DESC OFFSET {((pagina - 1) * tamano)} LIMIT {tamano}";

        var query = new QueryDefinition(queryText)
            .WithParameter("@usuarioId", usuarioId.ToString());

        if (!string.IsNullOrWhiteSpace(severidad))
            query = query.WithParameter("@severidad", severidad);

        if (desde.HasValue)
            query = query.WithParameter("@desde", desde.Value.ToString("O"));

        if (hasta.HasValue)
            query = query.WithParameter("@hasta", hasta.Value.ToString("O"));

        var results = new List<Incidente>();
        using var iterator = _container.GetItemQueryIterator<Incidente>(query);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }
        return results;
    }

    public async Task<int> CountFilteredAsync(Guid usuarioId, string? severidad, DateTime? desde, DateTime? hasta)
    {
        var whereClauses = new List<string> { "c.usuarioId = @usuarioId" };

        if (!string.IsNullOrWhiteSpace(severidad))
            whereClauses.Add("c.severidad = @severidad");

        if (desde.HasValue)
            whereClauses.Add("c.creadoEn >= @desde");

        if (hasta.HasValue)
            whereClauses.Add("c.creadoEn <= @hasta");

        var where = string.Join(" AND ", whereClauses);
        var query = new QueryDefinition($"SELECT VALUE COUNT(1) FROM c WHERE {where}")
            .WithParameter("@usuarioId", usuarioId.ToString());

        if (!string.IsNullOrWhiteSpace(severidad))
            query = query.WithParameter("@severidad", severidad);

        if (desde.HasValue)
            query = query.WithParameter("@desde", desde.Value.ToString("O"));

        if (hasta.HasValue)
            query = query.WithParameter("@hasta", hasta.Value.ToString("O"));

        using var iterator = _container.GetItemQueryIterator<int>(query);
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
        await _container.CreateItemAsync(incidente, new PartitionKey(incidente.UsuarioId.ToString()));
    }

    public async Task UpdateAsync(Incidente incidente)
    {
        await _container.UpsertItemAsync(incidente, new PartitionKey(incidente.UsuarioId.ToString()));
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
