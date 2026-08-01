namespace ImpactX.Infrastructure.Data;

/// <summary>
/// Indica un desajuste de esquema entre un contenedor Cosmos real y el
/// catálogo (p. ej. partition key path diferente). El mensaje es seguro:
/// solo contiene el nombre lógico del contenedor y una descripción
/// genérica. No se borra ni recrea el contenedor: requiere migración
/// controlada.
/// </summary>
public sealed class CosmosSchemaValidationException : Exception
{
    public CosmosSchemaValidationException(string containerName)
        : base($"Container '{containerName}' partition key does not match the catalog definition. Controlled migration required.")
    {
        ContainerName = containerName;
    }

    public string ContainerName { get; }
}
