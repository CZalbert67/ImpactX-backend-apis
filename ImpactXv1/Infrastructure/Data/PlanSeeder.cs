using System.Net;
using Microsoft.Azure.Cosmos;
using ImpactX.Core.Domain;

namespace ImpactX.Infrastructure.Data;

public static class PlanSeeder
{
    // IDs determinísticos: permiten point-read idempotente sin recorrer
    // el contenedor (SELECT * FROM c) ni duplicar planes entre ejecuciones.
    public static readonly Guid FreePlanId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid BasicPlanId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    public static readonly Guid PremiumPlanId = Guid.Parse("00000000-0000-0000-0000-000000000003");

    public static IReadOnlyList<Plan> SeedPlans { get; } =
    [
        new Plan
        {
            Id = FreePlanId,
            Nombre = "Free",
            PrecioMensual = 0,
            PrecioAnual = 0,
            MaxContactos = 2,
            MaxMonitores = 1,
            HistorialMapa = false,
            ExportacionDatos = false,
            SoportePrioritario = false,
            DuracionTrialDias = 0,
            SensoresHabilitados = ["G", "Mic", "PPG", "GPS"],
            BypassCritico = false,
            Telemetria = true,
            Descripcion = "Plan gratuito de prueba con sensores de impacto y ubicación.",
            TemporizadorSegundos = 12,
        },
        new Plan
        {
            Id = BasicPlanId,
            Nombre = "Basic",
            PrecioMensual = 99,
            PrecioAnual = 999,
            MaxContactos = 3,
            MaxMonitores = 3,
            HistorialMapa = false,
            ExportacionDatos = false,
            SoportePrioritario = false,
            DuracionTrialDias = 30,
            SensoresHabilitados = ["G", "GPS"],
            BypassCritico = false,
            Telemetria = false,
            Descripcion = "Plan básico para conductores frecuentes.",
            TemporizadorSegundos = 20,
        },
        new Plan
        {
            Id = PremiumPlanId,
            Nombre = "Premium",
            PrecioMensual = 199,
            PrecioAnual = 1999,
            MaxContactos = 8,
            MaxMonitores = -1,
            HistorialMapa = true,
            ExportacionDatos = true,
            SoportePrioritario = true,
            DuracionTrialDias = 30,
            SensoresHabilitados = ["G", "Mic", "PPG", "GPS", "Bypass"],
            BypassCritico = true,
            Telemetria = true,
            Descripcion = "Plan premium con monitoreo completo y bypass crítico.",
            TemporizadorSegundos = 10,
        },
    ];

    public static async Task SeedPlansAsync(CosmosDbContext cosmosDb, CancellationToken cancellationToken = default)
    {
        foreach (var plan in SeedPlans)
        {
            // Fast path: point-read por ID determinístico (sin escanear el contenedor).
            var existing = await cosmosDb.GetPlanByIdAsync(plan.Id, cancellationToken);
            if (existing is not null)
            {
                continue;
            }

            // Fallback para datos sembrados con IDs aleatorios antes de PR 2A:
            // COUNT pequeño y parametrizado por nombre evita duplicados.
            var countByName = await cosmosDb.CountPlansByNameAsync(plan.Nombre, cancellationToken);
            if (countByName > 0)
            {
                continue;
            }

            try
            {
                await cosmosDb.CreatePlanAsync(plan, cancellationToken);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
                // Siembra best-effort: conflicto de creación concurrente es esperado.
            }
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
                MaxContactos = 2,
                MaxMonitores = 1,
                HistorialMapa = false,
                ExportacionDatos = false,
                SoportePrioritario = false,
                DuracionTrialDias = 0,
                SensoresHabilitados = ["G", "Mic", "PPG", "GPS"],
                BypassCritico = false,
                Telemetria = true,
                Descripcion = "Plan gratuito de prueba con sensores de impacto y ubicación.",
                TemporizadorSegundos = 12,
            },
            new Plan
            {
                Id = Guid.NewGuid(),
                Nombre = "Basic",
                PrecioMensual = 99,
                PrecioAnual = 999,
                MaxContactos = 3,
                MaxMonitores = 3,
                HistorialMapa = false,
                ExportacionDatos = false,
                SoportePrioritario = false,
                DuracionTrialDias = 30,
                SensoresHabilitados = ["G", "GPS"],
                BypassCritico = false,
                Telemetria = false,
                Descripcion = "Plan básico para conductores frecuentes.",
                TemporizadorSegundos = 20,
            },
            new Plan
            {
                Id = Guid.NewGuid(),
                Nombre = "Premium",
                PrecioMensual = 199,
                PrecioAnual = 1999,
                MaxContactos = 8,
                MaxMonitores = -1,
                HistorialMapa = true,
                ExportacionDatos = true,
                SoportePrioritario = true,
                DuracionTrialDias = 30,
                SensoresHabilitados = ["G", "Mic", "PPG", "GPS", "Bypass"],
                BypassCritico = true,
                Telemetria = true,
                Descripcion = "Plan premium con monitoreo completo y bypass crítico.",
                TemporizadorSegundos = 10,
            }
        );

        await db.SaveChangesAsync();
    }
}
