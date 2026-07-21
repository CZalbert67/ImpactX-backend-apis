using ImpactX.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ImpactX.Extensions;

public static class WebApplicationExtensions
{
    public static async Task SeedDatabaseAsync(this WebApplication app, bool useCosmosDb, bool useInMemory)
    {
        if (useCosmosDb)
        {
            var cosmosDb = app.Services.GetRequiredService<CosmosDbContext>();
            await cosmosDb.EnsureContainersAsync();
            await PlanSeeder.SeedPlansAsync(cosmosDb);
        }
        else if (useInMemory)
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await PlanSeeder.SeedPlansEfAsync(db);
        }
    }
}
