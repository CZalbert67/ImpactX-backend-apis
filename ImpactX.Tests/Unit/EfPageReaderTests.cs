using ImpactX.Core.Exceptions;
using ImpactX.Core.Pagination;
using ImpactX.Infrastructure.Data.Repositories.EF;
using Microsoft.EntityFrameworkCore;

namespace ImpactX.Tests.Unit;

public class EfPageReaderTests
{
    private sealed class PageItem
    {
        public int Id { get; set; }
        public string Value { get; set; } = "";
    }

    private sealed class PageContext : DbContext
    {
        public PageContext(DbContextOptions<PageContext> options) : base(options)
        {
        }

        public DbSet<PageItem> Items { get; set; } = null!;
    }

    private static async Task<PageContext> CreateContextAsync(int itemCount)
    {
        var options = new DbContextOptionsBuilder<PageContext>()
            .UseInMemoryDatabase($"ef-page-reader-{Guid.NewGuid():N}")
            .Options;

        var context = new PageContext(options);
        for (int i = 1; i <= itemCount; i++)
        {
            context.Items.Add(new PageItem { Id = i, Value = $"item-{i}" });
        }
        await context.SaveChangesAsync();
        return context;
    }

    [Fact]
    public async Task ReadSinglePageAsync_ZeroItems_NoTokenNoHasMore()
    {
        await using var context = await CreateContextAsync(0);

        var page = await EfPageReader.ReadSinglePageAsync(
            context.Items.OrderBy(i => i.Id), 20, null);

        Assert.Empty(page.Items);
        Assert.False(page.HasMoreResults);
        Assert.Null(page.ContinuationToken);
    }

    [Fact]
    public async Task ReadSinglePageAsync_FewerThanPageSize_NoToken()
    {
        await using var context = await CreateContextAsync(3);

        var page = await EfPageReader.ReadSinglePageAsync(
            context.Items.OrderBy(i => i.Id), 20, null);

        Assert.Equal(3, page.Items.Count);
        Assert.False(page.HasMoreResults);
        Assert.Null(page.ContinuationToken);
    }

    [Fact]
    public async Task ReadSinglePageAsync_ExactlyPageSize_NoFalseToken()
    {
        await using var context = await CreateContextAsync(20);

        var page = await EfPageReader.ReadSinglePageAsync(
            context.Items.OrderBy(i => i.Id), 20, null);

        Assert.Equal(20, page.Items.Count);
        Assert.False(page.HasMoreResults);
        Assert.Null(page.ContinuationToken);
    }

    [Fact]
    public async Task ReadSinglePageAsync_PageSizePlusOne_HasMoreAndToken()
    {
        await using var context = await CreateContextAsync(21);

        var page = await EfPageReader.ReadSinglePageAsync(
            context.Items.OrderBy(i => i.Id), 20, null);

        Assert.Equal(20, page.Items.Count);
        Assert.True(page.HasMoreResults);
        Assert.NotNull(page.ContinuationToken);
    }

    [Fact]
    public async Task ReadSinglePageAsync_TwoFullPages_TokenAdvancesAndEnds()
    {
        await using var context = await CreateContextAsync(40);

        var first = await EfPageReader.ReadSinglePageAsync(
            context.Items.OrderBy(i => i.Id), 20, null);

        Assert.Equal(20, first.Items.Count);
        Assert.True(first.HasMoreResults);
        Assert.NotNull(first.ContinuationToken);

        var second = await EfPageReader.ReadSinglePageAsync(
            context.Items.OrderBy(i => i.Id), 20, first.ContinuationToken);

        Assert.Equal(20, second.Items.Count);
        Assert.False(second.HasMoreResults);
        Assert.Null(second.ContinuationToken);
        Assert.Equal(21, second.Items[0].Id);
    }

    [Fact]
    public async Task ReadSinglePageAsync_LastPageExact_NoToken()
    {
        await using var context = await CreateContextAsync(50);

        var first = await EfPageReader.ReadSinglePageAsync(
            context.Items.OrderBy(i => i.Id), 20, null);
        var second = await EfPageReader.ReadSinglePageAsync(
            context.Items.OrderBy(i => i.Id), 20, first.ContinuationToken);
        var third = await EfPageReader.ReadSinglePageAsync(
            context.Items.OrderBy(i => i.Id), 20, second.ContinuationToken);

        Assert.Equal(10, third.Items.Count);
        Assert.False(third.HasMoreResults);
        Assert.Null(third.ContinuationToken);
    }

    [Fact]
    public async Task ReadSinglePageAsync_LastPagePartial_NoToken()
    {
        await using var context = await CreateContextAsync(45);

        var first = await EfPageReader.ReadSinglePageAsync(
            context.Items.OrderBy(i => i.Id), 20, null);
        var second = await EfPageReader.ReadSinglePageAsync(
            context.Items.OrderBy(i => i.Id), 20, first.ContinuationToken);
        var third = await EfPageReader.ReadSinglePageAsync(
            context.Items.OrderBy(i => i.Id), 20, second.ContinuationToken);

        Assert.Equal(5, third.Items.Count);
        Assert.False(third.HasMoreResults);
        Assert.Null(third.ContinuationToken);
    }

    [Theory]
    [InlineData("not-base64!!")]
    [InlineData("b2Zmc2V0Oi0x")]
    [InlineData("b2Zmc2V0Og==")]
    [InlineData("MQ==")]
    public async Task ReadSinglePageAsync_InvalidOffsetToken_ThrowsBadRequest(string token)
    {
        await using var context = await CreateContextAsync(5);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            EfPageReader.ReadSinglePageAsync(
                context.Items.OrderBy(i => i.Id), 20, token));
    }
}
