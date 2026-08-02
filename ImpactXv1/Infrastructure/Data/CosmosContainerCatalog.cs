using Microsoft.Azure.Cosmos;

namespace ImpactX.Infrastructure.Data;

/// <summary>
/// Definición fuertemente tipada de un contenedor Cosmos DB.
/// Es la única fuente de verdad para creación y acceso a contenedores.
/// No porta throughput dedicado: todos los contenedores comparten el
/// throughput manual de la base (SharedThroughput).
/// </summary>
public sealed class CosmosContainerDefinition
{
    internal CosmosContainerDefinition(
        string name,
        string partitionKeyPath,
        int defaultTimeToLive = -1,
        string entity = "",
        IReadOnlyList<string[]>? compositeIndexes = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Container name must not be empty.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(partitionKeyPath) ||
            !partitionKeyPath.StartsWith('/') ||
            partitionKeyPath.Length < 2 ||
            partitionKeyPath.Contains(' ') ||
            partitionKeyPath.EndsWith('/') ||
            partitionKeyPath.IndexOf('/', 1) >= 0)
        {
            throw new ArgumentException(
                "Partition key path must be a single path segment starting with '/'.",
                nameof(partitionKeyPath));
        }

        if (defaultTimeToLive != -1 && defaultTimeToLive <= 0)
        {
            throw new ArgumentException(
                "DefaultTimeToLive must be -1 (no expiry) or a positive number of seconds.",
                nameof(defaultTimeToLive));
        }

        var normalizedIndexes = (compositeIndexes ?? [])
            .Select(paths =>
            {
                if (paths.Length == 0 || paths.Any(p => string.IsNullOrWhiteSpace(p) || !p.StartsWith('/')))
                {
                    throw new ArgumentException(
                        "Composite index paths must be non-empty and start with '/'.", nameof(compositeIndexes));
                }

                return paths.ToArray();
            })
            .ToArray();

        Name = name;
        PartitionKeyPath = partitionKeyPath;
        DefaultTimeToLive = defaultTimeToLive;
        Entity = entity;
        CompositeIndexes = normalizedIndexes;
    }

    public string Name { get; }
    public string PartitionKeyPath { get; }
    public int DefaultTimeToLive { get; }
    public string Entity { get; }
    public IReadOnlyList<string[]> CompositeIndexes { get; }

    public ContainerProperties ToContainerProperties()
        => new(Name, PartitionKeyPath)
        {
            DefaultTimeToLive = DefaultTimeToLive
        };
}

/// <summary>
/// Catálogo central de contenedores Cosmos DB.
/// Conserva los nombres y partition keys existentes para no romper
/// compatibilidad, y valida la definición completa al cargarse.
/// Ningún contenedor configura throughput dedicado.
/// </summary>
public static class CosmosContainerCatalog
{
    public static readonly CosmosContainerDefinition Usuarios =
        Create("Usuarios", "/id", -1, "Usuario");

    public static readonly CosmosContainerDefinition RefreshTokens =
        Create("RefreshTokens", "/usuarioId", 604800, "RefreshToken");

    public static readonly CosmosContainerDefinition PasswordResetTokens =
        Create("PasswordResetTokens", "/usuarioId", 3600, "PasswordResetToken");

    public static readonly CosmosContainerDefinition Dispositivos =
        Create("Dispositivos", "/usuarioId", -1, "Dispositivo");

    public static readonly CosmosContainerDefinition Planes =
        Create("Planes", "/id", -1, "Plan");

    public static readonly CosmosContainerDefinition Suscripciones =
        Create("Suscripciones", "/usuarioId", -1, "Suscripcion");

    public static readonly CosmosContainerDefinition Pagos =
        Create("Pagos", "/usuarioId", -1, "Pago");

    public static readonly CosmosContainerDefinition Monitores =
        Create("Monitores", "/usuarioId", -1, "Monitor");

    public static readonly CosmosContainerDefinition ContactosEmergencia =
        Create("ContactosEmergencia", "/usuarioId", -1, "ContactoEmergencia");

    public static readonly CosmosContainerDefinition Rutas =
        Create("Rutas", "/usuarioId", -1, "Ruta");

    public static readonly CosmosContainerDefinition Viajes =
        Create("Viajes", "/usuarioId", 7776000, "Viaje");

    public static readonly CosmosContainerDefinition TelemetriaViaje =
        Create("TelemetriaViaje", "/viajeId", 7776000, "ViajeTelemetry");

    public static readonly CosmosContainerDefinition Alertas =
        Create("Alertas", "/usuarioId", 31536000, "Alerta");

    public static readonly CosmosContainerDefinition Notificaciones =
        Create("Notificaciones", "/usuarioId", 2592000, "Notificacion");

    public static readonly CosmosContainerDefinition Wearables =
        Create("Wearables", "/usuarioId", -1, "Wearable");

    public static readonly CosmosContainerDefinition AppInvites =
        Create("AppInvites", "/usuarioId", 2592000, "AppInvite");

    public static readonly CosmosContainerDefinition ChatThreads =
        Create("ChatThreads", "/usuarioId", -1, "ChatThread");

    public static readonly CosmosContainerDefinition Incidentes =
        Create("Incidentes", "/usuarioId", -1, "Incidente");

    public static readonly CosmosContainerDefinition Vehicles =
        Create("Vehicles", "/ownerUserId", -1, "Vehicle");

    public static IReadOnlyList<CosmosContainerDefinition> All { get; }

    private static readonly Dictionary<string, CosmosContainerDefinition> ByName;

    static CosmosContainerCatalog()
    {
        All = new[]
        {
            Usuarios,
            RefreshTokens,
            PasswordResetTokens,
            Dispositivos,
            Planes,
            Suscripciones,
            Pagos,
            Monitores,
            ContactosEmergencia,
            Rutas,
            Viajes,
            TelemetriaViaje,
            Alertas,
            Notificaciones,
            Wearables,
            AppInvites,
            ChatThreads,
            Incidentes,
            Vehicles
        };

        ByName = new Dictionary<string, CosmosContainerDefinition>(StringComparer.Ordinal);

        foreach (var definition in All)
        {
            if (!ByName.TryAdd(definition.Name, definition))
            {
                throw new InvalidOperationException(
                    $"Duplicate container name in catalog: '{definition.Name}'.");
            }
        }
    }

    public static bool TryGet(string name, out CosmosContainerDefinition? definition)
        => ByName.TryGetValue(name, out definition);

    private static CosmosContainerDefinition Create(
        string name, string partitionKeyPath, int ttl, string entity)
        => new(name, partitionKeyPath, ttl, entity);
}
