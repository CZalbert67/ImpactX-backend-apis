using ImpactX.Core.Exceptions;
using ImpactX.Infrastructure.Data.Repositories.Cosmos;
using Microsoft.Azure.Cosmos;
using Moq;

namespace ImpactX.Tests.Unit;

public sealed class CosmosPageReaderTestsDummyDoc
{
    public string Id { get; set; } = "";
}

public class CosmosPageReaderTests
{
    private static (Mock<Container> container, Mock<FeedIterator<CosmosPageReaderTestsDummyDoc>> iterator, Mock<FeedResponse<CosmosPageReaderTestsDummyDoc>> response) CreateMocks(
        string? continuationToken, int itemCount, bool hasMore)
    {
        var container = new Mock<Container>();
        var iterator = new Mock<FeedIterator<CosmosPageReaderTestsDummyDoc>>();
        var response = new Mock<FeedResponse<CosmosPageReaderTestsDummyDoc>>();

        var resources = Enumerable.Range(0, itemCount)
            .Select(i => new CosmosPageReaderTestsDummyDoc { Id = $"doc-{i}" })
            .ToList();

        response.SetupGet(r => r.ContinuationToken).Returns(continuationToken);
        response.SetupGet(r => r.Resource).Returns(resources);
        iterator.SetupGet(i => i.HasMoreResults).Returns(hasMore);
        iterator.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(response.Object);

        container
            .Setup(c => c.GetItemQueryIterator<CosmosPageReaderTestsDummyDoc>(
                It.IsAny<QueryDefinition>(), It.IsAny<string>(), It.IsAny<QueryRequestOptions>()))
            .Returns(iterator.Object);

        return (container, iterator, response);
    }

    [Fact]
    public async Task ReadSinglePageAsync_ExactlyOneReadNextAsync()
    {
        var (container, iterator, _) = CreateMocks("token-1", 20, true);

        await CosmosPageReader.ReadSinglePageAsync<CosmosPageReaderTestsDummyDoc>(
            container.Object, new QueryDefinition("SELECT * FROM c"), PartitionKey.Null, 20, null, CancellationToken.None);

        iterator.Verify(i => i.ReadNextAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReadSinglePageAsync_MaxItemCount_EqualsPageSize()
    {
        var (container, _, _) = CreateMocks("token-1", 20, true);

        await CosmosPageReader.ReadSinglePageAsync<CosmosPageReaderTestsDummyDoc>(
            container.Object, new QueryDefinition("SELECT * FROM c"), PartitionKey.Null, 50, null, CancellationToken.None);

        container.Verify(c => c.GetItemQueryIterator<CosmosPageReaderTestsDummyDoc>(
            It.IsAny<QueryDefinition>(),
            It.IsAny<string>(),
            It.Is<QueryRequestOptions>(o => o.MaxItemCount == 50)), Times.Once);
    }

    [Fact]
    public async Task ReadSinglePageAsync_WithPartitionKey_SetsQueryRequestOptionsPartitionKey()
    {
        var (container, _, _) = CreateMocks("token-1", 5, false);
        var pk = new PartitionKey("usuario-123");

        await CosmosPageReader.ReadSinglePageAsync<CosmosPageReaderTestsDummyDoc>(
            container.Object, new QueryDefinition("SELECT * FROM c"), pk, 20, null, CancellationToken.None);

        container.Verify(c => c.GetItemQueryIterator<CosmosPageReaderTestsDummyDoc>(
            It.IsAny<QueryDefinition>(),
            It.IsAny<string>(),
            It.Is<QueryRequestOptions>(o => o.PartitionKey.HasValue && o.PartitionKey.Value == pk)), Times.Once);
    }

    [Fact]
    public async Task ReadSinglePageAsync_ContinuationToken_FromFeedResponseOnly()
    {
        var (container, _, _) = CreateMocks("cosmos-token-xyz", 20, true);

        var page = await CosmosPageReader.ReadSinglePageAsync<CosmosPageReaderTestsDummyDoc>(
            container.Object, new QueryDefinition("SELECT * FROM c"), PartitionKey.Null, 20, null, CancellationToken.None);

        Assert.Equal("cosmos-token-xyz", page.ContinuationToken);
        Assert.True(page.HasMoreResults);
        Assert.Equal(20, page.Items.Count);
    }

    [Fact]
    public async Task ReadSinglePageAsync_NoMoreResults_NullTokenAndHasMoreFalse()
    {
        var (container, _, _) = CreateMocks(null, 20, false);

        var page = await CosmosPageReader.ReadSinglePageAsync<CosmosPageReaderTestsDummyDoc>(
            container.Object, new QueryDefinition("SELECT * FROM c"), PartitionKey.Null, 20, null, CancellationToken.None);

        Assert.Null(page.ContinuationToken);
        Assert.False(page.HasMoreResults);
        Assert.Equal(20, page.Items.Count);
    }

    [Fact]
    public async Task ReadSinglePageAsync_CosmosToken_NotDecoded_NotModified()
    {
        var (container, _, _) = CreateMocks("continue+next/1=", 20, true);
        var inputToken = "continue+next/1=";

        await CosmosPageReader.ReadSinglePageAsync<CosmosPageReaderTestsDummyDoc>(
            container.Object, new QueryDefinition("SELECT * FROM c"), PartitionKey.Null, 20, inputToken, CancellationToken.None);

        container.Verify(c => c.GetItemQueryIterator<CosmosPageReaderTestsDummyDoc>(
            It.IsAny<QueryDefinition>(),
            It.Is<string>(t => t == "continue+next/1="),
            It.IsAny<QueryRequestOptions>()), Times.Once);
    }

    [Fact]
    public async Task ReadSinglePageAsync_CosmosBadRequest_MapsToGenericBadRequest()
    {
        var container = new Mock<Container>();
        var iterator = new Mock<FeedIterator<CosmosPageReaderTestsDummyDoc>>();
        iterator.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("invalid continuation token", System.Net.HttpStatusCode.BadRequest, 0, "activity-id", 0));

        container
            .Setup(c => c.GetItemQueryIterator<CosmosPageReaderTestsDummyDoc>(
                It.IsAny<QueryDefinition>(), It.IsAny<string>(), It.IsAny<QueryRequestOptions>()))
            .Returns(iterator.Object);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            CosmosPageReader.ReadSinglePageAsync<CosmosPageReaderTestsDummyDoc>(
                container.Object, new QueryDefinition("SELECT * FROM c"), PartitionKey.Null, 20, "bad-token", CancellationToken.None));

        Assert.DoesNotContain("bad-token", ex.Message);
        Assert.DoesNotContain("activity-id", ex.Message);
        Assert.DoesNotContain("invalid continuation", ex.Message);
    }

    [Fact]
    public async Task ReadSinglePageAsync_CosmosServerError_Propagates()
    {
        var container = new Mock<Container>();
        var iterator = new Mock<FeedIterator<CosmosPageReaderTestsDummyDoc>>();
        iterator.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("internal", System.Net.HttpStatusCode.InternalServerError, 0, "activity-id", 0));

        container
            .Setup(c => c.GetItemQueryIterator<CosmosPageReaderTestsDummyDoc>(
                It.IsAny<QueryDefinition>(), It.IsAny<string>(), It.IsAny<QueryRequestOptions>()))
            .Returns(iterator.Object);

        await Assert.ThrowsAsync<CosmosException>(() =>
            CosmosPageReader.ReadSinglePageAsync<CosmosPageReaderTestsDummyDoc>(
                container.Object, new QueryDefinition("SELECT * FROM c"), PartitionKey.Null, 20, "token", CancellationToken.None));
    }
}
