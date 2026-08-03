using System.Net;
using ImpactX.Infrastructure.Data;
using ImpactX.Tests.Support;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ImpactX.Tests.Unit;

public class CosmosInitializationServiceTests
{
    private sealed class TestableInitializationService : CosmosInitializationService
    {
        public Func<CancellationToken, Task>? OnInitialize { get; set; }
        public int InitializeCalls { get; private set; }

        public TestableInitializationService(
            TestCosmosDbContext cosmosDb,
            DatabaseInitializationState state,
            DatabaseInitializationOptions options)
            : base(cosmosDb, state, Options.Create(options), NullLogger<CosmosInitializationService>.Instance)
        {
        }

        protected override Task InitializeAsync(CancellationToken cancellationToken)
        {
            InitializeCalls++;
            return OnInitialize?.Invoke(cancellationToken) ?? base.InitializeAsync(cancellationToken);
        }

        public Task InvokeConfiguredInitializationAsync(CancellationToken cancellationToken = default)
            => base.InitializeAsync(cancellationToken);
    }

    private static DatabaseInitializationOptions CreateOptions(bool enabled = true, int maxAttempts = 3)
        => new()
        {
            Enabled = enabled,
            MaxAttempts = maxAttempts,
            RetryDelaySeconds = 0,
            TimeoutSeconds = 60
        };

    private static async Task WaitForTerminalStateAsync(DatabaseInitializationState state)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (state.Status is DatabaseInitializationStatus.Running or DatabaseInitializationStatus.NotStarted
               && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }
    }

    private static CosmosException TransientError() => new(
        "transient", HttpStatusCode.TooManyRequests, 0, "test-activity", 0);

    private static CosmosException NonTransientError() => new(
        "unauthorized", HttpStatusCode.Unauthorized, 0, "test-activity", 0);

    [Fact]
    public async Task Disabled_DoesNotRunInitialization()
    {
        var cosmosDb = new TestCosmosDbContext();
        var state = new DatabaseInitializationState();
        var service = new TestableInitializationService(cosmosDb, state, CreateOptions(enabled: false));

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(DatabaseInitializationStatus.NotStarted, state.Status);
        Assert.Equal(0, service.InitializeCalls);
        Assert.False(state.IsReady);
    }

    [Fact]
    public async Task SuccessfulInitialization_MarksSucceeded()
    {
        var cosmosDb = new TestCosmosDbContext();
        var state = new DatabaseInitializationState();
        var service = new TestableInitializationService(cosmosDb, state, CreateOptions());

        await service.StartAsync(CancellationToken.None);
        await WaitForTerminalStateAsync(state);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(DatabaseInitializationStatus.Succeeded, state.Status);
        Assert.True(state.IsReady);
        Assert.Equal(1, service.InitializeCalls);
        Assert.Null(state.FailureDescription);
    }

    [Fact]
    public async Task TransientErrors_RetryLimited_ThenSucceed()
    {
        var attempts = 0;
        var cosmosDb = new TestCosmosDbContext();
        var state = new DatabaseInitializationState();
        var service = new TestableInitializationService(cosmosDb, state, CreateOptions(maxAttempts: 3))
        {
            OnInitialize = _ =>
            {
                attempts++;
                return attempts <= 2 ? Task.FromException(TransientError()) : Task.CompletedTask;
            }
        };

        await service.StartAsync(CancellationToken.None);
        await WaitForTerminalStateAsync(state);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(DatabaseInitializationStatus.Succeeded, state.Status);
        Assert.Equal(3, attempts);
        Assert.Equal(3, state.Attempts);
    }

    [Fact]
    public async Task TransientErrors_Exhausted_FailsWithoutInfiniteRetry()
    {
        var cosmosDb = new TestCosmosDbContext();
        var state = new DatabaseInitializationState();
        var service = new TestableInitializationService(cosmosDb, state, CreateOptions(maxAttempts: 2))
        {
            OnInitialize = _ => Task.FromException(TransientError())
        };

        await service.StartAsync(CancellationToken.None);
        await WaitForTerminalStateAsync(state);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(DatabaseInitializationStatus.Failed, state.Status);
        Assert.False(state.IsReady);
        Assert.Equal(2, service.InitializeCalls);
        Assert.NotNull(state.FailureDescription);
    }

    [Fact]
    public async Task NonTransientError_DoesNotRetry()
    {
        var cosmosDb = new TestCosmosDbContext();
        var state = new DatabaseInitializationState();
        var service = new TestableInitializationService(cosmosDb, state, CreateOptions(maxAttempts: 3))
        {
            OnInitialize = _ => Task.FromException(NonTransientError())
        };

        await service.StartAsync(CancellationToken.None);
        await WaitForTerminalStateAsync(state);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(DatabaseInitializationStatus.Failed, state.Status);
        Assert.Equal(1, service.InitializeCalls);
        Assert.NotNull(state.FailureDescription);
    }

    [Fact]
    public async Task Timeout_RetriedLimited_ThenFails()
    {
        var cosmosDb = new TestCosmosDbContext();
        var state = new DatabaseInitializationState();
        var service = new TestableInitializationService(cosmosDb, state, CreateOptions(maxAttempts: 2))
        {
            OnInitialize = ct => Task.FromException(new OperationCanceledException(ct))
        };

        await service.StartAsync(CancellationToken.None);
        await WaitForTerminalStateAsync(state);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(DatabaseInitializationStatus.Failed, state.Status);
        Assert.Equal(2, service.InitializeCalls);
        Assert.NotNull(state.FailureDescription);
    }

    [Fact]
    public async Task Cancellation_StopsGracefully()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var cosmosDb = new TestCosmosDbContext();
        var state = new DatabaseInitializationState();
        var service = new TestableInitializationService(cosmosDb, state, CreateOptions())
        {
            OnInitialize = ct =>
                Task.FromException(new OperationCanceledException(ct))
        };

        await service.StartAsync(cts.Token);
        await service.StopAsync(CancellationToken.None);

        Assert.NotEqual(DatabaseInitializationStatus.Succeeded, state.Status);
        Assert.NotEqual(DatabaseInitializationStatus.Failed, state.Status);
    }

    [Fact]
    public async Task UnexpectedError_FailsWithoutInfiniteRetry()
    {
        var cosmosDb = new TestCosmosDbContext();
        var state = new DatabaseInitializationState();
        var service = new TestableInitializationService(cosmosDb, state, CreateOptions(maxAttempts: 3))
        {
            OnInitialize = _ => Task.FromException(new InvalidOperationException("boom"))
        };

        await service.StartAsync(CancellationToken.None);
        await WaitForTerminalStateAsync(state);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(DatabaseInitializationStatus.Failed, state.Status);
        Assert.Equal(1, service.InitializeCalls);
        Assert.NotNull(state.FailureDescription);
    }

    [Fact]
    public async Task ValidateOnly_ValidatesSchemaWithoutEnsuringContainers()
    {
        var cosmosDb = new TestCosmosDbContext();
        var state = new DatabaseInitializationState();
        var options = CreateOptions();
        options.Mode = DatabaseInitializationMode.ValidateOnly;
        var service = new TestableInitializationService(cosmosDb, state, options);

        await service.InvokeConfiguredInitializationAsync();

        Assert.Equal(1, cosmosDb.ValidateCalls);
        Assert.Equal(0, cosmosDb.EnsureCalls);
    }

    [Fact]
    public async Task EnsureMode_EnsuresContainersAndDoesNotRunValidateOnly()
    {
        var cosmosDb = new TestCosmosDbContext();
        var state = new DatabaseInitializationState();
        var options = CreateOptions();
        options.Mode = DatabaseInitializationMode.Ensure;
        var service = new TestableInitializationService(cosmosDb, state, options);

        await service.InvokeConfiguredInitializationAsync();

        Assert.Equal(1, cosmosDb.EnsureCalls);
        Assert.Equal(0, cosmosDb.ValidateCalls);
    }

}
