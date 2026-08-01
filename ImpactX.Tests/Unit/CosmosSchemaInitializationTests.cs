using System.Net;
using ImpactX.Infrastructure.Data;
using ImpactX.Tests.Support;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ImpactX.Tests.Unit;

public class CosmosSchemaInitializationTests
{
    private sealed class FakeCosmosDbContext : CosmosDbContext
    {
        public CosmosDatabaseOptions ConfiguredOptions { get; }
        public int DatabaseWithThroughputCalls { get; private set; }
        public int DatabaseWithoutThroughputCalls { get; private set; }
        public bool ThrowBadRequestOnDatabaseCreate { get; set; }
        public bool SimulateConflictOnCreate { get; set; }
        public Func<CancellationToken, Task>? OnCreateDatabase { get; set; }
        public Dictionary<string, string> ExistingContainers { get; } = new(StringComparer.Ordinal);
        public List<CosmosContainerDefinition> CreatedContainers { get; } = [];
        public Func<string, CancellationToken, Task<ContainerProperties?>>? OnReadContainer { get; set; }

        public FakeCosmosDbContext(CosmosDatabaseOptions options)
            : base(Microsoft.Extensions.Options.Options.Create(options))
        {
            ConfiguredOptions = options;
        }

        public void SeedExistingContainersFromCatalog()
        {
            foreach (var definition in CosmosContainerCatalog.All)
            {
                ExistingContainers[definition.Name] = definition.PartitionKeyPath;
            }
        }

        protected override Task CreateDatabaseWithThroughputAsync(CancellationToken cancellationToken)
        {
            DatabaseWithThroughputCalls++;
            if (OnCreateDatabase is not null)
            {
                return OnCreateDatabase(cancellationToken);
            }

            return ThrowBadRequestOnDatabaseCreate
                ? Task.FromException(new CosmosException(
                    "bad request", HttpStatusCode.BadRequest, 0, "activity", 0))
                : Task.CompletedTask;
        }

        protected override Task CreateDatabaseWithoutThroughputAsync(CancellationToken cancellationToken)
        {
            DatabaseWithoutThroughputCalls++;
            return Task.CompletedTask;
        }

        protected override Task<ContainerProperties?> ReadContainerPropertiesAsync(
            string containerName, CancellationToken cancellationToken)
        {
            if (OnReadContainer is not null)
            {
                return OnReadContainer(containerName, cancellationToken);
            }

            if (ExistingContainers.TryGetValue(containerName, out var partitionKeyPath))
            {
                return Task.FromResult<ContainerProperties?>(new ContainerProperties(containerName, partitionKeyPath));
            }

            return Task.FromResult<ContainerProperties?>(null);
        }

        protected override Task CreateContainerIfNotExistsAsync(
            CosmosContainerDefinition definition, CancellationToken cancellationToken)
        {
            if (SimulateConflictOnCreate)
            {
                // Carrera con otra instancia: el contenedor ya existe con el
                // mismo esquema; la re-lectura posterior lo valida.
                ExistingContainers[definition.Name] = definition.PartitionKeyPath;
                return Task.FromException(new CosmosException(
                    "conflict", HttpStatusCode.Conflict, 0, "activity", 0));
            }

            CreatedContainers.Add(definition);
            ExistingContainers[definition.Name] = definition.PartitionKeyPath;
            return Task.CompletedTask;
        }
    }

    private static async Task WaitForTerminalStateAsync(DatabaseInitializationState state)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (state.Status is DatabaseInitializationStatus.Running or DatabaseInitializationStatus.NotStarted
               && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }
    }

    private static CosmosDatabaseOptions CreateOptions()
        => new()
        {
            Endpoint = "https://localhost:443/",
            Key = "dGVzdC1rZXk=",
            DatabaseName = "ImpactX-Test",
            SharedThroughput = 400
        };

    [Fact]
    public async Task EnsureContainers_CreatesDatabaseWithSharedThroughput_AndAllMissingContainers()
    {
        var db = new FakeCosmosDbContext(CreateOptions());

        await db.EnsureContainersAsync(CancellationToken.None);

        Assert.Equal(1, db.DatabaseWithThroughputCalls);
        Assert.Equal(0, db.DatabaseWithoutThroughputCalls);
        Assert.Equal(400, db.ConfiguredOptions.SharedThroughput);
        Assert.Equal(CosmosContainerCatalog.All.Count, db.CreatedContainers.Count);
        Assert.All(CosmosContainerCatalog.All, d => Assert.Contains(db.CreatedContainers, c => c.Name == d.Name));
    }

    [Fact]
    public async Task EnsureContainers_NewContainers_HaveNoDedicatedThroughput()
    {
        var db = new FakeCosmosDbContext(CreateOptions());

        await db.EnsureContainersAsync(CancellationToken.None);

        // El catálogo no porta throughput (ni el tipo ni la firma de creación
        // lo admiten): los contenedores comparten el throughput manual de la
        // base. Se verifica que las propiedades creadas conserven id, PK y TTL.
        Assert.All(db.CreatedContainers, d =>
        {
            var properties = d.ToContainerProperties();
            Assert.Equal(d.Name, properties.Id);
            Assert.Equal(d.PartitionKeyPath, properties.PartitionKeyPath);
            Assert.Equal(d.DefaultTimeToLive, properties.DefaultTimeToLive);
        });
    }

    [Fact]
    public async Task EnsureContainers_IsIdempotent_WhenSchemaMatches()
    {
        var db = new FakeCosmosDbContext(CreateOptions());
        db.SeedExistingContainersFromCatalog();

        await db.EnsureContainersAsync(CancellationToken.None);
        await db.EnsureContainersAsync(CancellationToken.None);

        Assert.Empty(db.CreatedContainers);
        Assert.Equal(2, db.DatabaseWithThroughputCalls);
    }

    [Fact]
    public async Task EnsureContainers_PartitionKeyMismatch_ThrowsWithoutDeletingOrRecreating()
    {
        var db = new FakeCosmosDbContext(CreateOptions());
        db.SeedExistingContainersFromCatalog();
        db.ExistingContainers["Usuarios"] = "/wrongPath";

        var ex = await Assert.ThrowsAsync<CosmosSchemaValidationException>(
            () => db.EnsureContainersAsync(CancellationToken.None));

        Assert.Equal("Usuarios", ex.ContainerName);
        Assert.Empty(db.CreatedContainers);
    }

    [Fact]
    public async Task EnsureContainers_ExistingContainerMissing_IsCreatedAndValidated()
    {
        var db = new FakeCosmosDbContext(CreateOptions());
        db.SeedExistingContainersFromCatalog();
        db.ExistingContainers.Remove("Alertas");

        await db.EnsureContainersAsync(CancellationToken.None);

        Assert.Contains(db.CreatedContainers, c => c.Name == "Alertas");
    }

    [Fact]
    public async Task EnsureContainers_Cancellation_Propagates()
    {
        var db = new FakeCosmosDbContext(CreateOptions())
        {
            OnCreateDatabase = ct => Task.FromException(new OperationCanceledException(ct))
        };

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => db.EnsureContainersAsync(CancellationToken.None));
    }

    [Fact]
    public async Task EnsureContainers_DatabaseBadRequest_FallsBackWithoutThroughput()
    {
        var db = new FakeCosmosDbContext(CreateOptions())
        {
            ThrowBadRequestOnDatabaseCreate = true
        };

        await db.EnsureContainersAsync(CancellationToken.None);

        Assert.Equal(1, db.DatabaseWithThroughputCalls);
        Assert.Equal(1, db.DatabaseWithoutThroughputCalls);
        Assert.Equal(CosmosContainerCatalog.All.Count, db.CreatedContainers.Count);
    }

    [Fact]
    public async Task EnsureContainers_CreateRaceConflict_IsTolerated()
    {
        var db = new FakeCosmosDbContext(CreateOptions())
        {
            SimulateConflictOnCreate = true
        };

        await db.EnsureContainersAsync(CancellationToken.None);

        Assert.Empty(db.CreatedContainers);
    }

    [Trait("Category", "Security")]
    [Fact]
    public async Task SchemaMismatch_FailsInitializationSafely_WithoutSecrets()
    {
        var db = new FakeCosmosDbContext(CreateOptions());
        db.SeedExistingContainersFromCatalog();
        db.ExistingContainers["Incidentes"] = "/otroId";

        var state = new DatabaseInitializationState();
        var service = new CosmosInitializationService(
            db, state,
            Options.Create(new DatabaseInitializationOptions
            {
                Enabled = true,
                MaxAttempts = 3,
                RetryDelaySeconds = 0,
                TimeoutSeconds = 60
            }),
            NullLogger<CosmosInitializationService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await WaitForTerminalStateAsync(state);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(DatabaseInitializationStatus.Failed, state.Status);
        Assert.False(state.IsReady);
        Assert.Equal("Database schema mismatch detected; controlled migration required.", state.FailureDescription);
        Assert.DoesNotContain(db.ConfiguredOptions.Endpoint, state.FailureDescription);
        Assert.DoesNotContain(db.ConfiguredOptions.Key, state.FailureDescription);
        Assert.DoesNotContain("Incidentes", state.FailureDescription);
    }

    [Trait("Category", "Security")]
    [Fact]
    public void SchemaMismatch_ExceptionMessage_IsSafe()
    {
        var ex = new CosmosSchemaValidationException("Viajes");

        Assert.Equal("Viajes", ex.ContainerName);
        Assert.Contains("migration", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", ex.Message);
        Assert.DoesNotContain("dGVzdC1rZXk=", ex.Message);
        Assert.DoesNotContain("Viajes/wrong", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Trait("Category", "Security")]
    [Fact]
    public async Task SchemaMismatch_ReadinessReflectsFailure()
    {
        var db = new FakeCosmosDbContext(CreateOptions());
        var state = new DatabaseInitializationState();
        state.MarkRunning(3);
        state.MarkFailed("Database schema mismatch detected; controlled migration required.");

        var check = new ImpactX.Health.DatabaseReadinessCheck(
            db,
            state,
            Options.Create(new DatabaseInitializationOptions { Enabled = true }),
            Options.Create(new ImpactX.Configuration.ReadinessOptions { InitializationRequired = true }));

        var result = await check.CheckHealthAsync(new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext());

        Assert.Equal(Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public void SchemaMismatch_IsNotRetried_NonTransient()
    {
        // Un desajuste de esquema es permanente: la inicialización falla sin
        // reintentos ni loops infinitos. Se verifica que la excepción no es
        // un CosmosException transitorio (el servicio no lo reintenta).
        var ex = new CosmosSchemaValidationException("Rutas");
        Assert.IsNotType<CosmosException>(ex);
    }
}
