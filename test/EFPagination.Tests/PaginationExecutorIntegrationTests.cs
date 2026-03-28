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

        var page = await PaginationExecutor.ExecuteAsync(
            _dbContext.MainModels, definition,
            new ExecutionOptions(PageSize: 10, IncludeCount: true));

        page.Items.Select(x => x.Id).Should().BeEquivalentTo(Enumerable.Range(1, 10), options => options.WithStrictOrdering());
        page.HasNext.Should().BeTrue();
        page.TotalCount.Should().Be(99);
    }

    [Fact]
    public async Task ExecuteAsync_AfterReference_UsesKeysetFilter_AndSkipsCount()
    {
        var definition = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));
        var reference = await _dbContext.MainModels.OrderBy(x => x.Id).Skip(9).FirstAsync();

        var page = await PaginationExecutor.ExecuteAsync(
            _dbContext.MainModels, definition,
            new ExecutionOptions(PageSize: 10),
            reference);

        page.Items.Select(x => x.Id).Should().BeEquivalentTo(Enumerable.Range(11, 10), options => options.WithStrictOrdering());
        page.HasNext.Should().BeTrue();
        page.TotalCount.Should().Be(-1);
    }

    [Fact]
    public async Task ExecuteAsync_AfterColumnValues_UsesDirectValuePath_AndSkipsCount()
    {
        var definition = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Created).Ascending(x => x.Id));
        var reference = await _dbContext.MainModels.OrderBy(x => x.Created).ThenBy(x => x.Id).Skip(9).FirstAsync();
        ColumnValue[] referenceValues =
        [
            new(nameof(MainModel.Created), reference.Created),
            new(nameof(MainModel.Id), reference.Id),
        ];

        var page = await PaginationExecutor.ExecuteAsync(
            _dbContext.MainModels, definition,
            new ExecutionOptions(PageSize: 10),
            referenceValues);

        page.Items.Select(x => x.Id).Should().BeEquivalentTo(Enumerable.Range(11, 10), options => options.WithStrictOrdering());
        page.HasNext.Should().BeTrue();
        page.TotalCount.Should().Be(-1);
    }

    [Fact]
    public async Task ExecuteAsync_WithColumnValues_RejectsComputedDefinitions()
    {
        var definition = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.CreatedNullable ?? DateTime.MinValue).Ascending(x => x.Id));
        ColumnValue[] referenceValues =
        [
            new(nameof(MainModel.CreatedNullable), DateTime.MinValue),
            new(nameof(MainModel.Id), 10),
        ];

        var act = () => PaginationExecutor.ExecuteAsync(
            _dbContext.MainModels, definition,
            new ExecutionOptions(PageSize: 10),
            referenceValues);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Direct column-value pagination only supports member-access columns*");
    }

    [Fact]
    public async Task ExecuteAsync_LastPageOfFilteredQuery_ReturnsNoMore_AndFilteredCount()
    {
        var definition = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));
        var filtered = _dbContext.MainModels.Where(x => x.IsDone);
        var reference = await filtered.OrderBy(x => x.Id).Skip(39).FirstAsync();

        var page = await PaginationExecutor.ExecuteAsync(
            filtered, definition,
            new ExecutionOptions(PageSize: 10, IncludeCount: true),
            reference);

        page.Items.Select(x => x.Id).Should().BeEquivalentTo(s_expectedLastFilteredPageIds, options => options.WithStrictOrdering());
        page.HasNext.Should().BeFalse();
        page.TotalCount.Should().Be(49);
    }

    [Fact]
    public async Task ExecuteAsync_PreCanceledToken_ThrowsOperationCanceledException()
    {
        var definition = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => PaginationExecutor.ExecuteAsync(
            _dbContext.MainModels, definition,
            new ExecutionOptions(PageSize: 10, IncludeCount: true),
            ct: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ExecuteAsync_Backward_ReturnsReversedItems_HasMore()
    {
        var definition = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));
        var reference = await _dbContext.MainModels.OrderBy(x => x.Id).Skip(49).FirstAsync();

        var page = await PaginationExecutor.ExecuteAsync(
            _dbContext.MainModels, definition,
            new ExecutionOptions(PageSize: 10, Direction: PaginationDirection.Backward),
            reference);

        page.Items.Should().BeInAscendingOrder(x => x.Id);
        page.Items.Last().Id.Should().BeLessThan(reference.Id);
        page.HasNext.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_MaxPageSize_ClampsResult()
    {
        var definition = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));

        var page = await PaginationExecutor.ExecuteAsync(
            _dbContext.MainModels, definition,
            new ExecutionOptions(PageSize: 10_000, MaxPageSize: 5));

        page.Items.Should().HaveCountLessThanOrEqualTo(5);
    }
}
