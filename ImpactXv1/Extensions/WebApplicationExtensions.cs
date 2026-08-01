using ImpactX.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ImpactX.Extensions;

public static class WebApplicationExtensions
{
    public static async Task SeedDatabaseAsync(this WebApplication app, bool useCosmosDb, bool useInMemory)
    {
        if (useCosmosDb)
        {
            // La inicialización Cosmos (contenedores + planes) corre de forma asíncrona
            // en CosmosInitializationService para no bloquear el arranque del proceso.
            // Readiness (/health/ready) permanece Unhealthy hasta que termine.
            return;
        }

        if (useInMemory)
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await PlanSeeder.SeedPlansEfAsync(db);
        }
    }
}
