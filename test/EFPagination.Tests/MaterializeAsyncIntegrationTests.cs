using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using EFPagination.TestModels;
using Xunit;

namespace EFPagination;

[Collection(SqliteDatabaseCollection.Name)]
public class MaterializeAsyncIntegrationTests
{
    private readonly TestDbContext _dbContext;

    public MaterializeAsyncIntegrationTests(SqliteDatabaseFixture fixture)
    {
        var provider = fixture.BuildServices();
        _dbContext = provider.GetService<TestDbContext>();
    }

    [Fact]
    public async Task MaterializeAsync_Forward_FirstPage_HasPreviousFalse_HasNextTrue()
    {
        var def = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));
        var ctx = _dbContext.MainModels.Paginate(def);

        var result = await ctx.MaterializeAsync(10);

        result.HasPrevious.Should().BeFalse();
        result.HasNext.Should().BeTrue();
        result.Items.Should().HaveCount(10);
        result.Items.Select(x => x.Id).Should().BeEquivalentTo(Enumerable.Range(1, 10), o => o.WithStrictOrdering());
    }

    [Fact]
    public async Task MaterializeAsync_Forward_WithReference_HasPreviousTrue()
    {
        var def = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));
        var ctx = _dbContext.MainModels.Paginate(def, PaginationDirection.Forward, new { Id = 10 });

        var result = await ctx.MaterializeAsync(10);

        result.HasPrevious.Should().BeTrue();
        result.HasNext.Should().BeTrue();
        result.Items.First().Id.Should().BeGreaterThan(10);
    }

    [Fact]
    public async Task MaterializeAsync_Forward_LastPage_HasNextFalse()
    {
        var def = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));
        var ctx = _dbContext.MainModels.Paginate(def, PaginationDirection.Forward, new { Id = 90 });

        var result = await ctx.MaterializeAsync(20);

        result.HasPrevious.Should().BeTrue();
        result.HasNext.Should().BeFalse();
        result.Items.Should().HaveCountLessThan(20);
    }

    [Fact]
    public async Task MaterializeAsync_Backward_ReturnsCorrectOrder_WithNavigationFlags()
    {
        var def = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));
        var ctx = _dbContext.MainModels.Paginate(def, PaginationDirection.Backward, new { Id = 50 });

        var result = await ctx.MaterializeAsync(10);

        result.HasNext.Should().BeTrue();
        result.HasPrevious.Should().BeTrue();
        result.Items.Should().BeInAscendingOrder(x => x.Id);
        result.Items.Last().Id.Should().BeLessThan(50);
    }

    [Fact]
    public async Task MaterializeAsync_Backward_NearStart_HasPreviousFalse()
    {
        var def = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));
        var ctx = _dbContext.MainModels.Paginate(def, PaginationDirection.Backward, new { Id = 5 });

        var result = await ctx.MaterializeAsync(10);

        result.HasPrevious.Should().BeFalse();
        result.HasNext.Should().BeTrue();
        result.Items.Should().HaveCount(4);
    }

    [Fact]
    public async Task MaterializeAsync_EmptyResult_BothFlagsFalse()
    {
        var def = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));
        var ctx = _dbContext.MainModels.Where(x => x.Id > 9999).Paginate(def);

        var result = await ctx.MaterializeAsync(10);

        result.Items.Should().BeEmpty();
        result.HasPrevious.Should().BeFalse();
        result.HasNext.Should().BeFalse();
    }

    [Fact]
    public async Task MaterializeAsync_ThrowsOnInvalidPageSize()
    {
        var def = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));
        var ctx = _dbContext.MainModels.Paginate(def);

        var act = () => ctx.MaterializeAsync(0);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }
}
