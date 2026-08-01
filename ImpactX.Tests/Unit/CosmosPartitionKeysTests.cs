using ImpactX.Infrastructure.Data;
using Microsoft.Azure.Cosmos;

namespace ImpactX.Tests.Unit;

public class CosmosPartitionKeysTests
{
    [Trait("Category", "Security")]
    [Fact]
    public void ForGuid_MatchesContainerSerialization()
    {
        var id = Guid.NewGuid();
        var partitionKey = CosmosPartitionKeys.For(id);

        Assert.Equal(new PartitionKey(id.ToString()), partitionKey);
        // La representación SDK de PartitionKey es la forma JSON con corchetes.
        Assert.Equal($"[\"{id}\"]", partitionKey.ToString());
    }

    [Trait("Category", "Security")]
    [Fact]
    public void ForString_PreservesValue()
    {
        var value = "9f8a2c6e-0000-0000-0000-000000000000";
        Assert.Equal(new PartitionKey(value), CosmosPartitionKeys.For(value));
    }

    [Trait("Category", "Security")]
    [Fact]
    public void DifferentGuids_ProduceDifferentPartitionKeys()
    {
        var first = CosmosPartitionKeys.For(Guid.NewGuid());
        var second = CosmosPartitionKeys.For(Guid.NewGuid());

        Assert.NotEqual(first, second);
    }
}
