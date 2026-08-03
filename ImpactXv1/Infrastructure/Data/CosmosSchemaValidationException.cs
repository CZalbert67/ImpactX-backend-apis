namespace ImpactX.Infrastructure.Data;

public enum CosmosSchemaMismatchKind
{
    MissingContainer,
    PartitionKey,
    TimeToLive
}

/// <summary>
/// Desajuste seguro entre el esquema real y CosmosContainerCatalog. Nunca
/// contiene endpoint, key, partition key real ni valores de documentos.
/// </summary>
public sealed class CosmosSchemaValidationException : Exception
{
    public CosmosSchemaValidationException(string containerName)
        : this(containerName, CosmosSchemaMismatchKind.PartitionKey)
    {
    }

    public CosmosSchemaValidationException(
        string containerName,
        CosmosSchemaMismatchKind mismatchKind)
        : base($"Container '{containerName}' does not match the required schema. Controlled migration required.")
    {
        ContainerName = containerName;
        MismatchKind = mismatchKind;
    }

    public string ContainerName { get; }
    public CosmosSchemaMismatchKind MismatchKind { get; }
}
