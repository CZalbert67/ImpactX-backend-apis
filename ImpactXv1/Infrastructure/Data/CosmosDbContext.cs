using Microsoft.Azure.Cosmos;

namespace ImpactX.Infrastructure.Data;

public class CosmosDbContext
{
    private readonly CosmosClient _client;
    private readonly Database _database;

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

    public CosmosDbContext(IConfiguration config)
    {
        var endpoint = config["AzureCosmosDb:Endpoint"]!;
        var key = config["AzureCosmosDb:Key"]!;
        var dbName = config["AzureCosmosDb:DatabaseName"]!;

        _client = new CosmosClient(endpoint, key, new CosmosClientOptions
        {
            SerializerOptions = new CosmosSerializationOptions
            {
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
            }
        });

        _database = _client.GetDatabase(dbName);

        Usuarios = _database.GetContainer("Usuarios");
        RefreshTokens = _database.GetContainer("RefreshTokens");
        PasswordResetTokens = _database.GetContainer("PasswordResetTokens");
        Dispositivos = _database.GetContainer("Dispositivos");
        Planes = _database.GetContainer("Planes");
        Suscripciones = _database.GetContainer("Suscripciones");
        Pagos = _database.GetContainer("Pagos");
        Monitores = _database.GetContainer("Monitores");
        ContactosEmergencia = _database.GetContainer("ContactosEmergencia");
        Rutas = _database.GetContainer("Rutas");
        Viajes = _database.GetContainer("Viajes");
        TelemetriaViaje = _database.GetContainer("TelemetriaViaje");
        Alertas = _database.GetContainer("Alertas");
        Notificaciones = _database.GetContainer("Notificaciones");
        Wearables = _database.GetContainer("Wearables");
        AppInvites = _database.GetContainer("AppInvites");
        ChatThreads = _database.GetContainer("ChatThreads");
        Incidentes = _database.GetContainer("Incidentes");
    }

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

    public virtual async Task EnsureContainersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.CreateDatabaseIfNotExistsAsync(
                _database.Id,
                ThroughputProperties.CreateManualThroughput(400),
                cancellationToken: cancellationToken);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            // Cuenta incompatible con throughput manual (p. ej. serverless):
            // se reintenta sin throughput. 401/403/429/5xx propagan.
            await _client.CreateDatabaseIfNotExistsAsync(_database.Id, cancellationToken: cancellationToken);
        }

        var containerDefinitions = new[]
        {
            ("Usuarios", "/id", -1),
            ("RefreshTokens", "/usuarioId", 604800),
            ("PasswordResetTokens", "/usuarioId", 3600),
            ("Dispositivos", "/usuarioId", -1),
            ("Planes", "/id", -1),
            ("Suscripciones", "/usuarioId", -1),
            ("Pagos", "/usuarioId", -1),
            ("Monitores", "/usuarioId", -1),
            ("ContactosEmergencia", "/usuarioId", -1),
            ("Rutas", "/usuarioId", -1),
            ("Viajes", "/usuarioId", 7776000),
            ("TelemetriaViaje", "/viajeId", 7776000),
            ("Alertas", "/usuarioId", 31536000),
            ("Notificaciones", "/usuarioId", 2592000),
            ("Wearables", "/usuarioId", -1),
            ("AppInvites", "/usuarioId", 2592000),
            ("ChatThreads", "/usuarioId", -1),
            ("Incidentes", "/usuarioId", -1),
        };

        foreach (var (name, partitionKey, ttl) in containerDefinitions)
        {
            var properties = new ContainerProperties
            {
                Id = name,
                PartitionKeyPath = partitionKey,
                DefaultTimeToLive = ttl
            };

            await _database.CreateContainerIfNotExistsAsync(properties, cancellationToken: cancellationToken);
        }
    }
}
