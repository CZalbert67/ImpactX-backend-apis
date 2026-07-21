using Microsoft.Azure.Cosmos;
using ImpactX.Core.Domain;

namespace ImpactX.Infrastructure.Data;

public static class PlanSeeder
{
    public static async Task SeedPlansAsync(CosmosDbContext cosmosDb)
    {
        var container = cosmosDb.Planes;

        var existing = new List<Plan>();
        using var iterator = container.GetItemQueryIterator<Plan>("SELECT * FROM c");
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            existing.AddRange(response);
        }

        if (existing.Count > 0) return;

        var plans = new List<Plan>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Nombre = "Free",
                PrecioMensual = 0,
                PrecioAnual = 0,
                MaxContactos = 3,
                MaxMonitores = 1,
                HistorialMapa = false,
                ExportacionDatos = false,
                SoportePrioritario = false,
                DuracionTrialDias = 0,
            },
            new()
            {
                Id = Guid.NewGuid(),
                Nombre = "Basic",
                PrecioMensual = 99,
                PrecioAnual = 999,
                MaxContactos = 10,
                MaxMonitores = 3,
                HistorialMapa = false,
                ExportacionDatos = false,
                SoportePrioritario = false,
                DuracionTrialDias = 0,
            },
            new()
            {
                Id = Guid.NewGuid(),
                Nombre = "Premium",
                PrecioMensual = 199,
                PrecioAnual = 1999,
                MaxContactos = -1,
                MaxMonitores = -1,
                HistorialMapa = true,
                ExportacionDatos = true,
                SoportePrioritario = true,
                DuracionTrialDias = 0,
            },
        };

        foreach (var plan in plans)
        {
            await container.CreateItemAsync(plan, new PartitionKey(plan.Id.ToString()));
        }
    }

    public static async Task SeedPlansEfAsync(ApplicationDbContext db)
    {
        if (db.Planes.Any()) return;

        db.Planes.AddRange(
            new Plan
            {
                Id = Guid.NewGuid(),
                Nombre = "Free",
                PrecioMensual = 0,
                PrecioAnual = 0,
                MaxContactos = 3,
                MaxMonitores = 1,
                HistorialMapa = false,
                ExportacionDatos = false,
                SoportePrioritario = false,
                DuracionTrialDias = 0,
            },
            new Plan
            {
                Id = Guid.NewGuid(),
                Nombre = "Basic",
                PrecioMensual = 99,
                PrecioAnual = 999,
                MaxContactos = 10,
                MaxMonitores = 3,
                HistorialMapa = false,
                ExportacionDatos = false,
                SoportePrioritario = false,
                DuracionTrialDias = 0,
            },
            new Plan
            {
                Id = Guid.NewGuid(),
                Nombre = "Premium",
                PrecioMensual = 199,
                PrecioAnual = 1999,
                MaxContactos = -1,
                MaxMonitores = -1,
                HistorialMapa = true,
                ExportacionDatos = true,
                SoportePrioritario = true,
                DuracionTrialDias = 0,
            }
        );

        await db.SaveChangesAsync();
    }
}
