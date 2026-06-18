using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using EFPagination.TestModels;
using Xunit;

namespace EFPagination;

[Collection(SqliteDatabaseCollection.Name)]
public class CursorExecutorIntegrationTests
{
    private readonly TestDbContext _dbContext;

    public CursorExecutorIntegrationTests(SqliteDatabaseFixture fixture)
    {
        var provider = fixture.BuildServices();
        _dbContext = provider.GetService<TestDbContext>();
    }

    [Fact]
    public async Task ExecuteFromCursorAsync_FirstPage_HasNextCursor_NoPreviousCursor()
    {
        var def = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));

        var page = await PaginationExecutor.ExecuteFromCursorAsync(
            _dbContext.MainModels, def,
            new ExecutionOptions(PageSize: 10),
            cursor: []);

        page.Items.Should().HaveCount(10);
        page.NextCursor.Should().NotBeNullOrEmpty();
        page.PreviousCursor.Should().BeNull();
        page.Items.Select(x => x.Id).Should().BeEquivalentTo(Enumerable.Range(1, 10), o => o.WithStrictOrdering());
    }

    [Fact]
    public async Task ExecuteFromCursorAsync_FullPaginationLoop_CoversAllItems()
    {
        var def = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));
        var allItems = new List<MainModel>();
        string cursor = null!;

        do
        {
            var page = await PaginationExecutor.ExecuteFromCursorAsync(
                _dbContext.MainModels, def,
                new ExecutionOptions(PageSize: 10),
                cursor);

            allItems.AddRange(page.Items);
            cursor = page.NextCursor;
        } while (cursor is not null);

        allItems.Should().HaveCount(99);
        allItems.Select(x => x.Id).Should().BeInAscendingOrder();
        allItems.Select(x => x.Id).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task ExecuteFromCursorAsync_MiddlePage_HasBothCursors()
    {
        var def = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));

        var firstPage = await PaginationExecutor.ExecuteFromCursorAsync(
            _dbContext.MainModels, def,
            new ExecutionOptions(PageSize: 10),
            cursor: []);

        var secondPage = await PaginationExecutor.ExecuteFromCursorAsync(
            _dbContext.MainModels, def,
            new ExecutionOptions(PageSize: 10),
            firstPage.NextCursor);

        secondPage.NextCursor.Should().NotBeNullOrEmpty();
        secondPage.PreviousCursor.Should().NotBeNullOrEmpty();
        secondPage.Items.Select(x => x.Id).Should().BeEquivalentTo(Enumerable.Range(11, 10), o => o.WithStrictOrdering());
    }

    [Fact]
    public async Task ExecuteFromCursorAsync_BackwardToFirstPage_HasNoPreviousCursor_AndHasNextCursor()
    {
        var def = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));

        var firstPage = await PaginationExecutor.ExecuteFromCursorAsync(
            _dbContext.MainModels, def,
            new ExecutionOptions(PageSize: 10),
            cursor: []);

        var secondPage = await PaginationExecutor.ExecuteFromCursorAsync(
            _dbContext.MainModels, def,
            new ExecutionOptions(PageSize: 10),
            firstPage.NextCursor);

        var back = await PaginationExecutor.ExecuteFromCursorAsync(
            _dbContext.MainModels, def,
            new ExecutionOptions(PageSize: 10, Direction: PaginationDirection.Backward),
            secondPage.PreviousCursor);

        back.Items.Select(x => x.Id).Should().BeEquivalentTo(Enumerable.Range(1, 10), o => o.WithStrictOrdering());
        back.PreviousCursor.Should().BeNull();
        back.NextCursor.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExecuteFromCursorAsync_BackwardWithoutCursor_ReturnsLastPageWithPreviousCursor_AndNoNextCursor()
    {
        var def = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));

        var page = await PaginationExecutor.ExecuteFromCursorAsync(
            _dbContext.MainModels, def,
            new ExecutionOptions(PageSize: 10, Direction: PaginationDirection.Backward),
            cursor: []);

        page.Items.Select(x => x.Id).Should().BeEquivalentTo(Enumerable.Range(90, 10), o => o.WithStrictOrdering());
        page.PreviousCursor.Should().NotBeNullOrEmpty();
        page.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteFromCursorAsync_LastPage_HasNullNextCursor()
    {
        var def = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));
        string cursor = null!;
        CursorPage<MainModel> page;

        do
        {
            page = await PaginationExecutor.ExecuteFromCursorAsync(
                _dbContext.MainModels, def,
                new ExecutionOptions(PageSize: 50),
                cursor);
            cursor = page.NextCursor;
        } while (cursor is not null);

        page.NextCursor.Should().BeNull();
        page.PreviousCursor.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExecuteFromCursorAsync_InvalidCursor_ThrowsArgumentException()
    {
        var def = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));

        var act = () => PaginationExecutor.ExecuteFromCursorAsync(
            _dbContext.MainModels, def,
            new ExecutionOptions(PageSize: 10),
            "not-a-valid-cursor");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*cursor*");
    }

    [Fact]
    public async Task ExecuteFromCursorAsync_StringOverload_WorksIdentically()
    {
        var def = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));

        var page = await PaginationExecutor.ExecuteFromCursorAsync(
            _dbContext.MainModels, def,
            new ExecutionOptions(PageSize: 10),
            cursor: null!);

        page.Items.Should().HaveCount(10);
        page.NextCursor.Should().NotBeNullOrEmpty();

        var secondPage = await PaginationExecutor.ExecuteFromCursorAsync(
            _dbContext.MainModels, def,
            new ExecutionOptions(PageSize: 10),
            cursor: page.NextCursor);

        secondPage.Items.First().Id.Should().Be(11);
    }

    [Fact]
    public async Task ExecuteFromCursorAsync_WithIncludeCount_ReturnsTotalCount()
    {
        var def = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));

        var page = await PaginationExecutor.ExecuteFromCursorAsync(
            _dbContext.MainModels, def,
            new ExecutionOptions(PageSize: 10, IncludeCount: true),
            cursor: []);

        page.TotalCount.Should().Be(99);
    }

    [Fact]
    public async Task ExecuteFromCursorAsync_WithIncludeCount_ReusesCursorCount_WithoutSecondCountQuery()
    {
        var def = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));

        var firstPage = await PaginationExecutor.ExecuteFromCursorAsync(
            _dbContext.MainModels, def,
            new ExecutionOptions(PageSize: 10, IncludeCount: true),
            cursor: []);

        firstPage.TotalCount.Should().Be(99);
        var countLogsAfterFirst = CountQueryLogCount();
        countLogsAfterFirst.Should().BeGreaterThan(0, "the first (cursor-less) page issues a COUNT(*)");

        var secondPage = await PaginationExecutor.ExecuteFromCursorAsync(
            _dbContext.MainModels, def,
            new ExecutionOptions(PageSize: 10, IncludeCount: true),
            cursor: firstPage.NextCursor);

        secondPage.TotalCount.Should().Be(99);
        CountQueryLogCount().Should().Be(countLogsAfterFirst,
            "a cursor-carried count must be reused instead of re-running COUNT(*)");
    }

    // One COUNT(*) query surfaces across several log events (executing/executed); the absolute
    // tally is irrelevant — what matters is that a cursor-carried page adds no further ones.
    private int CountQueryLogCount()
        => _dbContext.LogMessages.Count(m => m.Contains("COUNT(*)", StringComparison.OrdinalIgnoreCase));

    [Fact]
    public async Task ExecuteFromCursorAsync_MultiColumn_RoundTripsCorrectly()
    {
        var def = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Created).Ascending(x => x.Id));
        var allItems = new List<MainModel>();
        string cursor = null!;

        do
        {
            var page = await PaginationExecutor.ExecuteFromCursorAsync(
                _dbContext.MainModels, def,
                new ExecutionOptions(PageSize: 25),
                cursor);

            allItems.AddRange(page.Items);
            cursor = page.NextCursor;
        } while (cursor is not null);

        allItems.Should().HaveCount(99);
        allItems.Select(x => x.Id).Should().OnlyHaveUniqueItems();
    }
}
