using ImpactX.Infrastructure.Data;

namespace ImpactX.Tests.Unit;

public class CosmosContainerCatalogTests
{
    private static readonly string[] ExpectedContainerNames =
    [
        "Usuarios",
        "RefreshTokens",
        "PasswordResetTokens",
        "Dispositivos",
        "Planes",
        "Suscripciones",
        "Pagos",
        "Monitores",
        "ContactosEmergencia",
        "Rutas",
        "Viajes",
        "TelemetriaViaje",
        "Alertas",
        "Notificaciones",
        "Wearables",
        "AppInvites",
        "ChatThreads",
        "Incidentes"
    ];

    [Fact]
    public void Catalog_ContainsAllKnownContainers()
    {
        Assert.Equal(ExpectedContainerNames.Length, CosmosContainerCatalog.All.Count);

        foreach (var name in ExpectedContainerNames)
        {
            Assert.Contains(CosmosContainerCatalog.All, d => d.Name == name);
        }
    }

    [Fact]
    public void Catalog_NoDuplicateNames()
    {
        var names = CosmosContainerCatalog.All.Select(d => d.Name).ToArray();
        Assert.Equal(names.Length, names.Distinct().Count());
    }

    [Fact]
    public void Catalog_NamesAreNotEmpty()
    {
        Assert.All(CosmosContainerCatalog.All, d => Assert.False(string.IsNullOrWhiteSpace(d.Name)));
    }

    [Fact]
    public void Catalog_PartitionKeyPathsAreValid()
    {
        Assert.All(CosmosContainerCatalog.All, d =>
        {
            Assert.StartsWith("/", d.PartitionKeyPath);
            Assert.True(d.PartitionKeyPath.Length > 1);
            Assert.DoesNotContain(" ", d.PartitionKeyPath);
            Assert.False(d.PartitionKeyPath.EndsWith('/'));
            Assert.Equal(-1, d.PartitionKeyPath.IndexOf('/', 1));
        });
    }

    [Fact]
    public void Catalog_NoContainerConfiguresDedicatedThroughput()
    {
        // El catálogo es la única fuente de creación y no expone throughput:
        // las propiedades convertidas solo portan id, partition key y TTL.
        foreach (var definition in CosmosContainerCatalog.All)
        {
            var properties = definition.ToContainerProperties();
            Assert.Equal(definition.Name, properties.Id);
            Assert.Equal(definition.PartitionKeyPath, properties.PartitionKeyPath);
        }
    }

    [Fact]
    public void Catalog_TimeToLiveIsValid_WhenConfigured()
    {
        Assert.All(CosmosContainerCatalog.All, d =>
        {
            Assert.True(d.DefaultTimeToLive == -1 || d.DefaultTimeToLive > 0,
                $"Container {d.Name} has invalid TTL {d.DefaultTimeToLive}");
        });
    }

    [Fact]
    public void Catalog_ExpectedTimeToLiveValues_ArePreserved()
    {
        Assert.Equal(-1, Get("Usuarios").DefaultTimeToLive);
        Assert.Equal(604800, Get("RefreshTokens").DefaultTimeToLive);
        Assert.Equal(3600, Get("PasswordResetTokens").DefaultTimeToLive);
        Assert.Equal(7776000, Get("Viajes").DefaultTimeToLive);
        Assert.Equal(7776000, Get("TelemetriaViaje").DefaultTimeToLive);
        Assert.Equal(31536000, Get("Alertas").DefaultTimeToLive);
        Assert.Equal(2592000, Get("Notificaciones").DefaultTimeToLive);
        Assert.Equal(2592000, Get("AppInvites").DefaultTimeToLive);
    }

    [Fact]
    public void Catalog_PartitionKeyPaths_ArePreserved()
    {
        Assert.Equal("/id", Get("Usuarios").PartitionKeyPath);
        Assert.Equal("/id", Get("Planes").PartitionKeyPath);
        Assert.Equal("/viajeId", Get("TelemetriaViaje").PartitionKeyPath);
        Assert.All(CosmosContainerCatalog.All.Where(d => d.Name is not "Usuarios" and not "Planes" and not "TelemetriaViaje"),
            d => Assert.Equal("/usuarioId", d.PartitionKeyPath));
    }

    [Fact]
    public void Catalog_NoCompositeIndexesDefined_NoneAreNeeded()
    {
        // Todas las consultas con ORDER BY están acotadas a una partición
        // (QueryRequestOptions.PartitionKey), por lo que Cosmos no requiere
        // composite indexes. No se agregan índices arbitrarios.
        Assert.All(CosmosContainerCatalog.All, d => Assert.Empty(d.CompositeIndexes));
    }

    [Fact]
    public void Definition_RejectsEmptyName()
    {
        Assert.Throws<ArgumentException>(() => new CosmosContainerDefinition(" ", "/id"));
    }

    [Fact]
    public void Definition_RejectsInvalidPartitionKeyPaths()
    {
        Assert.Throws<ArgumentException>(() => new CosmosContainerDefinition("Test", ""));
        Assert.Throws<ArgumentException>(() => new CosmosContainerDefinition("Test", "id"));
        Assert.Throws<ArgumentException>(() => new CosmosContainerDefinition("Test", "/"));
        Assert.Throws<ArgumentException>(() => new CosmosContainerDefinition("Test", "/a b"));
        Assert.Throws<ArgumentException>(() => new CosmosContainerDefinition("Test", "/a/b"));
        Assert.Throws<ArgumentException>(() => new CosmosContainerDefinition("Test", "/a/"));
    }

    [Fact]
    public void Definition_RejectsInvalidTimeToLive()
    {
        Assert.Throws<ArgumentException>(() => new CosmosContainerDefinition("Test", "/id", 0));
        Assert.Throws<ArgumentException>(() => new CosmosContainerDefinition("Test", "/id", -5));
        Assert.Throws<ArgumentException>(() => new CosmosContainerDefinition("Test", "/id", int.MinValue));
    }

    [Fact]
    public void Definition_AcceptsValidTimeToLive()
    {
        Assert.Equal(-1, new CosmosContainerDefinition("Test", "/id").DefaultTimeToLive);
        Assert.Equal(3600, new CosmosContainerDefinition("Test", "/id", 3600).DefaultTimeToLive);
    }

    [Fact]
    public void Definition_RejectsInvalidCompositeIndexPaths()
    {
        Assert.Throws<ArgumentException>(() =>
            new CosmosContainerDefinition("Test", "/id", -1, "Test", [Array.Empty<string>()]));
        Assert.Throws<ArgumentException>(() =>
            new CosmosContainerDefinition("Test", "/id", -1, "Test", [["campo"]]));
    }

    [Fact]
    public void TryGet_ReturnsDefinitionForKnownContainer()
    {
        Assert.True(CosmosContainerCatalog.TryGet("Alertas", out var definition));
        Assert.NotNull(definition);
        Assert.Equal("/usuarioId", definition.PartitionKeyPath);

        Assert.False(CosmosContainerCatalog.TryGet("NoExiste", out _));
    }

    private static CosmosContainerDefinition Get(string name)
        => CosmosContainerCatalog.All.Single(d => d.Name == name);
}
