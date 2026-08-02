using System.Net;
using ImpactX.Core.Domain;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace ImpactX.Infrastructure.Data;

public class CosmosDbContext
{
    private readonly CosmosClient _client;
    private readonly Database _database;
    private readonly CosmosDatabaseOptions _options;

    public Container Usuarios { get; }
    public Container RefreshTokens { get; }
    public Container PasswordResetTokens { get; }
    public Container Dispositivos { get; }
    public Container Planes { get; }
    public Container Suscripciones { get; }
    public Container Pagos { get; }
    public Container Monitores { get; }
    public Container ContactosEmergencia { get; }
    public Container Rutas { get; }
    public Container Viajes { get; }
    public Container TelemetriaViaje { get; }
    public Container Alertas { get; }
    public Container Notificaciones { get; }
    public Container Wearables { get; }
    public Container AppInvites { get; }
    public Container ChatThreads { get; }
    public Container Incidentes { get; }
    public Container Vehicles { get; }

    public CosmosDbContext(IOptions<CosmosDatabaseOptions> options)
    {
        _options = options.Value;

        _client = new CosmosClient(_options.Endpoint, _options.Key, new CosmosClientOptions
        {
            SerializerOptions = new CosmosSerializationOptions
            {
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
            },
            // Reintentos prudentes y limitados para 429: sin reintentos infinitos.
            RequestTimeout = TimeSpan.FromSeconds(_options.RequestTimeoutSeconds),
            MaxRetryAttemptsOnRateLimitedRequests = _options.MaxRetryAttemptsOnRateLimitedRequests,
            MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(_options.MaxRetryWaitTimeSeconds)
        });

        _database = _client.GetDatabase(_options.DatabaseName);

        Usuarios = GetContainer(CosmosContainerCatalog.Usuarios);
        RefreshTokens = GetContainer(CosmosContainerCatalog.RefreshTokens);
        PasswordResetTokens = GetContainer(CosmosContainerCatalog.PasswordResetTokens);
        Dispositivos = GetContainer(CosmosContainerCatalog.Dispositivos);
        Planes = GetContainer(CosmosContainerCatalog.Planes);
        Suscripciones = GetContainer(CosmosContainerCatalog.Suscripciones);
        Pagos = GetContainer(CosmosContainerCatalog.Pagos);
        Monitores = GetContainer(CosmosContainerCatalog.Monitores);
        ContactosEmergencia = GetContainer(CosmosContainerCatalog.ContactosEmergencia);
        Rutas = GetContainer(CosmosContainerCatalog.Rutas);
        Viajes = GetContainer(CosmosContainerCatalog.Viajes);
        TelemetriaViaje = GetContainer(CosmosContainerCatalog.TelemetriaViaje);
        Alertas = GetContainer(CosmosContainerCatalog.Alertas);
        Notificaciones = GetContainer(CosmosContainerCatalog.Notificaciones);
        Wearables = GetContainer(CosmosContainerCatalog.Wearables);
        AppInvites = GetContainer(CosmosContainerCatalog.AppInvites);
        ChatThreads = GetContainer(CosmosContainerCatalog.ChatThreads);
        Incidentes = GetContainer(CosmosContainerCatalog.Incidentes);
        Vehicles = GetContainer(CosmosContainerCatalog.Vehicles);
    }

    private Container GetContainer(CosmosContainerDefinition definition)
        => _database.GetContainer(definition.Name);

    public virtual async Task<bool> IsAccessibleAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _database.ReadAsync(cancellationToken: cancellationToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (CosmosException)
        {
            // No se registra Message (puede contener datos internos); solo se reporta inaccesible.
            return false;
        }
    }

    /// <summary>
    /// Crea la base (con throughput manual compartido) y los contenedores
    /// faltantes usando el catálogo. Idempotente: no borra, no recrea y no
    /// modifica contenedores existentes. Si un contenedor existente tiene
    /// un partition key path distinto al catálogo, lanza
    /// CosmosSchemaValidationException (migración controlada requerida).
    /// </summary>
    public virtual async Task EnsureContainersAsync(CancellationToken cancellationToken = default)
    {
        await EnsureDatabaseAsync(cancellationToken);

        foreach (var definition in CosmosContainerCatalog.All)
        {
            await EnsureContainerAsync(definition, cancellationToken);
        }
    }

    protected virtual async Task EnsureDatabaseAsync(CancellationToken cancellationToken)
    {
        try
        {
            await CreateDatabaseWithThroughputAsync(cancellationToken);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            // Cuenta incompatible con throughput manual (p. ej. serverless):
            // se reintenta sin throughput. 401/403/429/5xx propagan.
            await CreateDatabaseWithoutThroughputAsync(cancellationToken);
        }
    }

    protected virtual Task CreateDatabaseWithThroughputAsync(CancellationToken cancellationToken)
        => _client.CreateDatabaseIfNotExistsAsync(
            _database.Id,
            ThroughputProperties.CreateManualThroughput(_options.SharedThroughput),
            cancellationToken: cancellationToken);

    protected virtual Task CreateDatabaseWithoutThroughputAsync(CancellationToken cancellationToken)
        => _client.CreateDatabaseIfNotExistsAsync(_database.Id, cancellationToken: cancellationToken);

    protected virtual async Task EnsureContainerAsync(
        CosmosContainerDefinition definition, CancellationToken cancellationToken)
    {
        var existing = await ReadContainerPropertiesAsync(definition.Name, cancellationToken);

        if (existing is null)
        {
            try
            {
                await CreateContainerIfNotExistsAsync(definition, cancellationToken);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
                // Carrera con otra instancia: la re-lectura posterior valida
                // el esquema del contenedor creado por la otra instancia.
            }

            existing = await ReadContainerPropertiesAsync(definition.Name, cancellationToken);
        }

        if (existing is null)
        {
            throw new CosmosSchemaValidationException(definition.Name);
        }

        if (!string.Equals(existing.PartitionKeyPath, definition.PartitionKeyPath, StringComparison.OrdinalIgnoreCase))
        {
            // No se borra ni se recrea: requiere migración controlada.
            throw new CosmosSchemaValidationException(definition.Name);
        }
    }

    protected virtual async Task<ContainerProperties?> ReadContainerPropertiesAsync(
        string containerName, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _database.GetContainer(containerName)
                .ReadContainerAsync(cancellationToken: cancellationToken);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    protected virtual Task CreateContainerIfNotExistsAsync(
        CosmosContainerDefinition definition, CancellationToken cancellationToken)
    {
        // Sin throughput dedicado: el contenedor comparte el throughput de la base.
        return _database.CreateContainerIfNotExistsAsync(
            definition.ToContainerProperties(), cancellationToken: cancellationToken);
    }

    // PlanSeeder: operaciones virtuales para poder probar idempotencia
    // sin contactar Cosmos real.

    public virtual async Task<Plan?> GetPlanByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await Planes.ReadItemAsync<Plan>(
                id.ToString(), CosmosPartitionKeys.For(id), cancellationToken: cancellationToken);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public virtual async Task<int> CountPlansByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT VALUE COUNT(1) FROM c WHERE c.nombre = @name")
            .WithParameter("@name", name);

        using var iterator = Planes.GetItemQueryIterator<int>(query,
            requestOptions: new QueryRequestOptions { MaxItemCount = 1 });
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            return response.FirstOrDefault();
        }

        return 0;
    }

    public virtual async Task CreatePlanAsync(Plan plan, CancellationToken cancellationToken = default)
    {
        await Planes.CreateItemAsync(plan,
            CosmosPartitionKeys.For(plan.Id), cancellationToken: cancellationToken);
    }
}
