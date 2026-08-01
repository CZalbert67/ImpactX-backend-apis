using Microsoft.Azure.Cosmos;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Infrastructure.Data;

namespace ImpactX.Infrastructure.Data.Repositories.Cosmos;

public class CosmosPlanRepository : IPlanRepository
{
    private readonly Container _container;

    public CosmosPlanRepository(CosmosDbContext dbContext)
    {
        _container = dbContext.Planes;
    }

    public async Task<List<Plan>> GetAllAsync()
    {
        // Cross-partition justificada: catálogo global pequeño; la partición
        // es /id. Se pagina con MaxItemCount prudente.
        var query = new QueryDefinition("SELECT * FROM c");
        var plans = new List<Plan>();
        using var iterator = _container.GetItemQueryIterator<Plan>(query,
            requestOptions: new QueryRequestOptions { MaxItemCount = 100 });
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            plans.AddRange(response);
        }
        return plans;
    }

    public async Task<Plan?> GetByIdAsync(Guid id)
    {
        try
        {
            var response = await _container.ReadItemAsync<Plan>(
                id.ToString(), CosmosPartitionKeys.For(id));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<Plan?> GetByNameAsync(string name)
    {
        // Cross-partition justificada: búsqueda por nombre sin id conocido;
        // la partición es /id. Detención temprana.
        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.nombre = @name")
            .WithParameter("@name", name);

        using var iterator = _container.GetItemQueryIterator<Plan>(query,
            requestOptions: new QueryRequestOptions { MaxItemCount = 1 });
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }
        return null;
    }
}
