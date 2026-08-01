using Microsoft.Azure.Cosmos;

namespace ImpactX.Infrastructure.Data;

/// <summary>
/// Construcción centralizada de PartitionKey para los repositorios Cosmos.
/// Garantiza que id y partition key se serialicen de forma consistente
/// (Guid -> string) en todas las operaciones.
/// </summary>
public static class CosmosPartitionKeys
{
    public static PartitionKey For(Guid value) => new(value.ToString());

    public static PartitionKey For(string value) => new(value);
}
