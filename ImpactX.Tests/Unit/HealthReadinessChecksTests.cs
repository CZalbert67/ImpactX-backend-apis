using ImpactX.Configuration;
using ImpactX.Health;
using ImpactX.Infrastructure.Data;
using ImpactX.Tests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace ImpactX.Tests.Unit;

public class ConfigurationReadinessCheckTests
{
    private static IConfiguration CreateConfig(params (string Key, string? Value)[] values)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    private static async Task<HealthCheckResult> RunAsync(ConfigurationReadinessCheck check)
        => await check.CheckHealthAsync(new HealthCheckContext());

    [Fact]
    public async Task Healthy_WithoutCosmos()
    {
        var check = new ConfigurationReadinessCheck(CreateConfig(
            ("Jwt:Secret", "this-is-a-test-secret-that-is-32-bytes-long!"),
            ("UseCosmosDb", "false")));

        Assert.Equal(HealthStatus.Healthy, (await RunAsync(check)).Status);
    }

    [Fact]
    public async Task Healthy_WithValidCosmosConfiguration()
    {
        var check = new ConfigurationReadinessCheck(CreateConfig(
            ("Jwt:Secret", "this-is-a-test-secret-that-is-32-bytes-long!"),
            ("UseCosmosDb", "true"),
            ("AzureCosmosDb:Endpoint", "https://localhost:443/"),
            ("AzureCosmosDb:DatabaseName", "ImpactX-Data"),
            ("AzureCosmosDb:Key", "test-key")));

        Assert.Equal(HealthStatus.Healthy, (await RunAsync(check)).Status);
    }

    [Fact]
    public async Task MissingJwtSecret_Unhealthy()
    {
        var check = new ConfigurationReadinessCheck(CreateConfig(
            ("UseCosmosDb", "false")));

        var result = await RunAsync(check);
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Description);
        Assert.DoesNotContain("jwt", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PlaceholderCosmosKey_Unhealthy()
    {
        var check = new ConfigurationReadinessCheck(CreateConfig(
            ("Jwt:Secret", "this-is-a-test-secret-that-is-32-bytes-long!"),
            ("UseCosmosDb", "true"),
            ("AzureCosmosDb:Endpoint", "https://localhost:443/"),
            ("AzureCosmosDb:DatabaseName", "ImpactX-Data"),
            ("AzureCosmosDb:Key", "YOUR_AZURE_COSMOS_KEY")));

        Assert.Equal(HealthStatus.Unhealthy, (await RunAsync(check)).Status);
    }

    [Fact]
    public async Task MissingCosmosKey_Unhealthy()
    {
        var check = new ConfigurationReadinessCheck(CreateConfig(
            ("Jwt:Secret", "this-is-a-test-secret-that-is-32-bytes-long!"),
            ("UseCosmosDb", "true"),
            ("AzureCosmosDb:Endpoint", "https://localhost:443/"),
            ("AzureCosmosDb:DatabaseName", "ImpactX-Data")));

        Assert.Equal(HealthStatus.Unhealthy, (await RunAsync(check)).Status);
    }

    [Fact]
    public async Task MissingCosmosEndpoint_Unhealthy()
    {
        var check = new ConfigurationReadinessCheck(CreateConfig(
            ("Jwt:Secret", "this-is-a-test-secret-that-is-32-bytes-long!"),
            ("UseCosmosDb", "true"),
            ("AzureCosmosDb:DatabaseName", "ImpactX-Data"),
            ("AzureCosmosDb:Key", "test-key")));

        Assert.Equal(HealthStatus.Unhealthy, (await RunAsync(check)).Status);
    }

    [Fact]
    public async Task MissingCosmosDatabaseName_Unhealthy()
    {
        var check = new ConfigurationReadinessCheck(CreateConfig(
            ("Jwt:Secret", "this-is-a-test-secret-that-is-32-bytes-long!"),
            ("UseCosmosDb", "true"),
            ("AzureCosmosDb:Endpoint", "https://localhost:443/"),
            ("AzureCosmosDb:Key", "test-key")));

        Assert.Equal(HealthStatus.Unhealthy, (await RunAsync(check)).Status);
    }
}

public class DatabaseReadinessCheckTests
{
    private static readonly DatabaseInitializationOptions InitEnabled = new() { Enabled = true };
    private static readonly DatabaseInitializationOptions InitDisabled = new() { Enabled = false };
    private static readonly ReadinessOptions ReadinessRequired = new() { InitializationRequired = true };
    private static readonly ReadinessOptions ReadinessNotRequired = new() { InitializationRequired = false };

    private static DatabaseReadinessCheck CreateCheck(
        TestCosmosDbContext cosmosDb,
        DatabaseInitializationState state,
        DatabaseInitializationOptions initOptions,
        ReadinessOptions readinessOptions)
        => new(
            cosmosDb,
            state,
            Options.Create(initOptions),
            Options.Create(readinessOptions));

    private static async Task<HealthCheckResult> RunAsync(DatabaseReadinessCheck check)
        => await check.CheckHealthAsync(new HealthCheckContext());

    [Fact]
    public async Task InitRequired_NotStarted_Unhealthy()
    {
        var check = CreateCheck(new TestCosmosDbContext(), new DatabaseInitializationState(),
            InitEnabled, ReadinessRequired);

        var result = await RunAsync(check);
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("not completed", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InitRequired_Running_Unhealthy()
    {
        var state = new DatabaseInitializationState();
        state.MarkRunning(3);

        var check = CreateCheck(new TestCosmosDbContext(), state, InitEnabled, ReadinessRequired);

        Assert.Equal(HealthStatus.Unhealthy, (await RunAsync(check)).Status);
    }

    [Fact]
    public async Task InitRequired_Failed_Unhealthy()
    {
        var state = new DatabaseInitializationState();
        state.MarkRunning(2);
        state.MarkFailed("Database initialization failed after retries.");

        var check = CreateCheck(new TestCosmosDbContext(), state, InitEnabled, ReadinessRequired);

        var result = await RunAsync(check);
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("Database initialization failed after retries.", result.Description);
    }

    [Fact]
    public async Task InitSucceeded_Accessible_Healthy()
    {
        var state = new DatabaseInitializationState();
        state.MarkRunning(1);
        state.MarkSucceeded();

        var check = CreateCheck(new TestCosmosDbContext(), state, InitEnabled, ReadinessRequired);

        Assert.Equal(HealthStatus.Healthy, (await RunAsync(check)).Status);
    }

    [Fact]
    public async Task InitSucceeded_NotAccessible_Unhealthy()
    {
        var state = new DatabaseInitializationState();
        state.MarkRunning(1);
        state.MarkSucceeded();

        var check = CreateCheck(new TestCosmosDbContext { AccessCheck = _ => Task.FromResult(false) },
            state, InitEnabled, ReadinessRequired);

        Assert.Equal(HealthStatus.Unhealthy, (await RunAsync(check)).Status);
    }

    [Fact]
    public async Task InitDisabled_Accessible_Healthy()
    {
        var check = CreateCheck(new TestCosmosDbContext(), new DatabaseInitializationState(),
            InitDisabled, ReadinessRequired);

        Assert.Equal(HealthStatus.Healthy, (await RunAsync(check)).Status);
    }

    [Fact]
    public async Task InitDisabled_NotAccessible_Unhealthy()
    {
        var check = CreateCheck(new TestCosmosDbContext { AccessCheck = _ => Task.FromResult(false) },
            new DatabaseInitializationState(), InitDisabled, ReadinessRequired);

        Assert.Equal(HealthStatus.Unhealthy, (await RunAsync(check)).Status);
    }

    [Fact]
    public async Task AccessCheckTimeout_Unhealthy()
    {
        var check = CreateCheck(
            new TestCosmosDbContext
            {
                AccessCheck = _ => Task.FromException<bool>(new OperationCanceledException())
            },
            new DatabaseInitializationState(), InitDisabled, ReadinessRequired);

        var result = await RunAsync(check);
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("timed out", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InitNotRequired_PendingInitialization_StillChecksAccess()
    {
        var state = new DatabaseInitializationState();
        state.MarkRunning(3);

        var check = CreateCheck(new TestCosmosDbContext(), state, InitEnabled, ReadinessNotRequired);

        Assert.Equal(HealthStatus.Healthy, (await RunAsync(check)).Status);
    }
}
