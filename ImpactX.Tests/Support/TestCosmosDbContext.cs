using ImpactX.Core.Domain;
using ImpactX.Infrastructure.Data;
using Microsoft.Extensions.Options;

namespace ImpactX.Tests.Support;

public class TestCosmosDbContext : CosmosDbContext
{
    public Func<CancellationToken, Task<bool>>? AccessCheck { get; set; }
    public Func<CancellationToken, Task>? ContainerInitialization { get; set; }
    public Func<CancellationToken, Task>? SchemaValidation { get; set; }
    public int EnsureCalls { get; private set; }
    public int ValidateCalls { get; private set; }
    public Dictionary<Guid, Plan> PlansById { get; } = [];

    public TestCosmosDbContext() : base(CreateOptions())
    {
    }

    public override Task<bool> IsAccessibleAsync(CancellationToken cancellationToken = default)
        => AccessCheck?.Invoke(cancellationToken) ?? Task.FromResult(true);

    public override Task EnsureContainersAsync(CancellationToken cancellationToken = default)
    {
        EnsureCalls++;
        return ContainerInitialization?.Invoke(cancellationToken) ?? Task.CompletedTask;
    }

    public override Task ValidateSchemaAsync(CancellationToken cancellationToken = default)
    {
        ValidateCalls++;
        return SchemaValidation?.Invoke(cancellationToken) ?? Task.CompletedTask;
    }

    public override Task<Plan?> GetPlanByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
        => Task.FromResult(PlansById.TryGetValue(id, out var plan) ? plan : null);

    public override Task<int> CountPlansByNameAsync(
        string name,
        CancellationToken cancellationToken = default)
        => Task.FromResult(PlansById.Values.Count(plan =>
            string.Equals(plan.Nombre, name, StringComparison.OrdinalIgnoreCase)));

    public override Task CreatePlanAsync(
        Plan plan,
        CancellationToken cancellationToken = default)
    {
        PlansById[plan.Id] = plan;
        return Task.CompletedTask;
    }

    private static IOptions<CosmosDatabaseOptions> CreateOptions()
        => Options.Create(new CosmosDatabaseOptions
        {
            Endpoint = "https://localhost:443/",
            Key = "dGVzdC1rZXk=",
            DatabaseName = "ImpactX-Test"
        });
}
