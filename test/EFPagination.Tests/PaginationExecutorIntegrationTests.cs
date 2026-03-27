using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EFPagination.TestModels;
using Xunit;

namespace EFPagination;

[Collection(SqliteDatabaseCollection.Name)]
public class PaginationExecutorIntegrationTests
{
    private static readonly int[] s_expectedLastFilteredPageIds = [82, 84, 86, 88, 90, 92, 94, 96, 98];

    private readonly TestDbContext _dbContext;

    public PaginationExecutorIntegrationTests(SqliteDatabaseFixture fixture)
    {
        var provider = fixture.BuildServices();
        _dbContext = provider.GetService<TestDbContext>();
    }

    [Fact]
    public async Task ExecuteAsync_FirstPage_ReturnsTrimmedItems_HasMore_AndCount()
    {
        var definition = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));

        var page = await PaginationExecutor.ExecuteAsync(_dbContext.MainModels, definition, 10, null, includeCount: true);

        page.Items.Select(x => x.Id).Should().BeEquivalentTo(Enumerable.Range(1, 10), options => options.WithStrictOrdering());
        page.HasMore.Should().BeTrue();
        page.TotalCount.Should().Be(99);
    }

    [Fact]
    public async Task ExecuteAsync_AfterReference_UsesKeysetFilter_AndSkipsCount()
    {
        var definition = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));
        var reference = await _dbContext.MainModels.OrderBy(x => x.Id).Skip(9).FirstAsync();

        var page = await PaginationExecutor.ExecuteAsync(_dbContext.MainModels, definition, 10, reference, includeCount: false);

        page.Items.Select(x => x.Id).Should().BeEquivalentTo(Enumerable.Range(11, 10), options => options.WithStrictOrdering());
        page.HasMore.Should().BeTrue();
        page.TotalCount.Should().Be(-1);
    }

    [Fact]
    public async Task ExecuteAsync_LastPageOfFilteredQuery_ReturnsNoMore_AndFilteredCount()
    {
        var definition = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));
        var filtered = _dbContext.MainModels.Where(x => x.IsDone);
        var reference = await filtered.OrderBy(x => x.Id).Skip(39).FirstAsync();

        var page = await PaginationExecutor.ExecuteAsync(filtered, definition, 10, reference, includeCount: true);

        page.Items.Select(x => x.Id).Should().BeEquivalentTo(s_expectedLastFilteredPageIds, options => options.WithStrictOrdering());
        page.HasMore.Should().BeFalse();
        page.TotalCount.Should().Be(49);
    }

    [Fact]
    public async Task ExecuteAsync_PreCanceledToken_ThrowsOperationCanceledException()
    {
        var definition = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => PaginationExecutor.ExecuteAsync(_dbContext.MainModels, definition, 10, null, includeCount: true, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
