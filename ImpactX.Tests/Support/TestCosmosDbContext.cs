using ImpactX.Infrastructure.Data;
using Microsoft.Extensions.Configuration;

namespace ImpactX.Tests.Support;

public class TestCosmosDbContext : CosmosDbContext
{
    public Func<CancellationToken, Task<bool>>? AccessCheck { get; set; }
    public Func<CancellationToken, Task>? ContainerInitialization { get; set; }
    public int EnsureCalls { get; private set; }

    public TestCosmosDbContext() : base(CreateConfig())
    {
    }

    public override Task<bool> IsAccessibleAsync(CancellationToken cancellationToken = default)
        => AccessCheck?.Invoke(cancellationToken) ?? Task.FromResult(true);

    public override Task EnsureContainersAsync(CancellationToken cancellationToken = default)
    {
        EnsureCalls++;
        return ContainerInitialization?.Invoke(cancellationToken) ?? Task.CompletedTask;
    }

    private static IConfiguration CreateConfig()
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureCosmosDb:Endpoint"] = "https://localhost:443/",
                ["AzureCosmosDb:Key"] = "dGVzdC1rZXk=",
                ["AzureCosmosDb:DatabaseName"] = "ImpactX-Test"
            })
            .Build();
}
