using Microsoft.Azure.Cosmos;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;

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
        var query = new QueryDefinition("SELECT * FROM c");
        var plans = new List<Plan>();
        using var iterator = _container.GetItemQueryIterator<Plan>(query);
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
                id.ToString(), new PartitionKey(id.ToString()));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<Plan?> GetByNameAsync(string name)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.nombre = @name")
            .WithParameter("@name", name);

        using var iterator = _container.GetItemQueryIterator<Plan>(query);
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }
        return null;
    }
}
