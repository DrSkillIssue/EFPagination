using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using EFPagination.TestModels;
using Xunit;

namespace EFPagination;

[Collection(SqliteDatabaseCollection.Name)]
public class StreamingPaginationIntegrationTests
{
    private readonly TestDbContext _dbContext;

    public StreamingPaginationIntegrationTests(SqliteDatabaseFixture fixture)
    {
        var provider = fixture.BuildServices();
        _dbContext = provider.GetService<TestDbContext>();
    }

    [Fact]
    public async Task PaginateAllAsync_IteratesAllPages_NoDuplicates()
    {
        var def = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));
        var allItems = new List<MainModel>();

        await foreach (var page in PaginationStreaming.PaginateAllAsync(_dbContext.MainModels, def, 10))
        {
            allItems.AddRange(page);
        }

        allItems.Should().HaveCount(99);
        allItems.Select(x => x.Id).Should().OnlyHaveUniqueItems();
        allItems.Select(x => x.Id).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task PaginateAllAsync_SmallPageSize_StillCoversAll()
    {
        var def = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));
        var pageCount = 0;
        var totalItems = 0;

        await foreach (var page in PaginationStreaming.PaginateAllAsync(_dbContext.MainModels, def, 7))
        {
            pageCount++;
            totalItems += page.Count;
            page.Should().HaveCountLessThanOrEqualTo(7);
        }

        totalItems.Should().Be(99);
        pageCount.Should().Be(15);
    }

    [Fact]
    public async Task PaginateAllAsync_EmptyDataset_YieldsNothing()
    {
        var def = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));
        var pages = new List<List<MainModel>>();

        await foreach (var page in PaginationStreaming.PaginateAllAsync(
            _dbContext.MainModels.Where(x => x.Id > 9999), def, 10))
        {
            pages.Add(page);
        }

        pages.Should().BeEmpty();
    }

    [Fact]
    public async Task PaginateAllAsync_MultiColumn_MaintainsOrder()
    {
        var def = PaginationQuery.Build<MainModel>(b => b.Descending(x => x.Created).Ascending(x => x.Id));
        var allIds = new List<int>();

        await foreach (var page in PaginationStreaming.PaginateAllAsync(_dbContext.MainModels, def, 20))
        {
            allIds.AddRange(page.Select(x => x.Id));
        }

        allIds.Should().HaveCount(99);
        allIds.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task PaginateAllAsync_Cancellation_StopsIteration()
    {
        var def = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));
        using var cts = new CancellationTokenSource();
        var pagesReceived = 0;

        var act = async () =>
        {
            await foreach (var page in PaginationStreaming.PaginateAllAsync(_dbContext.MainModels, def, 10, cts.Token))
            {
                pagesReceived++;
                if (pagesReceived == 2)
                    await cts.CancelAsync();
            }
        };

        await act.Should().ThrowAsync<OperationCanceledException>();

        pagesReceived.Should().BeGreaterThanOrEqualTo(2);
        pagesReceived.Should().BeLessThan(10);
    }
}
