#nullable enable
using EFPagination.TestModels;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EFPagination;

[Collection(SqliteDatabaseCollection.Name)]
public class ServerSideProjectionIntegrationTests
{
    private readonly TestDbContext _db;

    public ServerSideProjectionIntegrationTests(SqliteDatabaseFixture fixture)
    {
        var provider = fixture.BuildServices();
        _db = provider.GetService<TestDbContext>()!;
    }

    private sealed record ItemDto(int EntityId, string String, DateTime Created);

    [Fact]
    public async Task TakeAsync_ProjectedSingleKey_ReturnsTypedItemsWithCursor()
    {
        var def = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));

        var page = await _db.MainModels
            .Keyset(def)
            .TakeAsync(10, x => new ItemDto(x.Id, x.String, x.Created));

        page.Items.Should().HaveCount(10);
        page.Items.Select(i => i.EntityId).Should().BeEquivalentTo(Enumerable.Range(1, 10), o => o.WithStrictOrdering());
        page.NextCursor.Should().NotBeNullOrEmpty();
        page.PreviousCursor.Should().BeNull();
    }

    [Fact]
    public async Task TakeAsync_ProjectedSingleKey_AdvancesByCursor()
    {
        var def = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));

        var first = await _db.MainModels.Keyset(def)
            .TakeAsync(10, x => new ItemDto(x.Id, x.String, x.Created));
        var second = await _db.MainModels.Keyset(def).After(first.NextCursor!)
            .TakeAsync(10, x => new ItemDto(x.Id, x.String, x.Created));

        second.Items.Should().HaveCount(10);
        second.Items.Select(i => i.EntityId).Should().BeEquivalentTo(Enumerable.Range(11, 10), o => o.WithStrictOrdering());
        second.PreviousCursor.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task TakeAsync_ProjectedTwoKey_RoundTripsCursors()
    {
        var def = PaginationQuery.Build<MainModel>(b => b
            .Ascending(x => x.Created)
            .Ascending(x => x.Id));

        var first = await _db.MainModels.Keyset(def)
            .TakeAsync(10, x => new ItemDto(x.Id, x.String, x.Created));
        var second = await _db.MainModels.Keyset(def).After(first.NextCursor!)
            .TakeAsync(10, x => new ItemDto(x.Id, x.String, x.Created));

        var firstIds = first.Items.Select(i => i.EntityId).ToList();
        var secondIds = second.Items.Select(i => i.EntityId).ToList();
        firstIds.Intersect(secondIds).Should().BeEmpty();
        secondIds.Should().HaveCount(10);
    }

    [Fact]
    public async Task TakeAsync_ProjectedThreeKey_RoundTripsCursors()
    {
        var def = PaginationQuery.Build<MainModel>(b => b
            .Ascending(x => x.IsDone)
            .Ascending(x => x.Created)
            .Ascending(x => x.Id));

        var first = await _db.MainModels.Keyset(def)
            .TakeAsync(10, x => new ItemDto(x.Id, x.String, x.Created));
        var second = await _db.MainModels.Keyset(def).After(first.NextCursor!)
            .TakeAsync(10, x => new ItemDto(x.Id, x.String, x.Created));

        first.Items.Select(i => i.EntityId).Intersect(second.Items.Select(i => i.EntityId)).Should().BeEmpty();
        second.Items.Should().HaveCount(10);
    }

    [Fact]
    public async Task TakeAsync_ProjectedBackwardDirection_ReturnsPreviousPage()
    {
        var def = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));

        var first = await _db.MainModels.Keyset(def)
            .TakeAsync(10, x => new ItemDto(x.Id, x.String, x.Created));
        var second = await _db.MainModels.Keyset(def).After(first.NextCursor!)
            .TakeAsync(10, x => new ItemDto(x.Id, x.String, x.Created));
        var back = await _db.MainModels.Keyset(def).Before(second.PreviousCursor!)
            .TakeAsync(10, x => new ItemDto(x.Id, x.String, x.Created));

        back.Items.Select(i => i.EntityId).Should().BeEquivalentTo(Enumerable.Range(1, 10), o => o.WithStrictOrdering());
        back.PreviousCursor.Should().BeNull();
        back.NextCursor.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task TakeAsync_Projected_RenamedKeyColumns_AreNotRequiredOnDto()
    {
        var def = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));

        // ItemDto has no "Id" property — only EntityId. The cursor encoding goes through the
        // typed envelope, not via reading DTO properties by name.
        var first = await _db.MainModels.Keyset(def)
            .TakeAsync(10, x => new ItemDto(x.Id, x.String, x.Created));
        var second = await _db.MainModels.Keyset(def).After(first.NextCursor!)
            .TakeAsync(10, x => new ItemDto(x.Id, x.String, x.Created));

        second.Items.Select(i => i.EntityId).Should().BeEquivalentTo(Enumerable.Range(11, 10), o => o.WithStrictOrdering());
    }

    [Fact]
    public async Task TakeAsync_Projected_LastPage_NextCursorIsNull()
    {
        var def = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));

        var first = await _db.MainModels.Keyset(def)
            .TakeAsync(50, x => new ItemDto(x.Id, x.String, x.Created));
        var page = first;
        while (page.NextCursor is not null)
        {
            page = await _db.MainModels.Keyset(def).After(page.NextCursor)
                .TakeAsync(50, x => new ItemDto(x.Id, x.String, x.Created));
        }

        page.NextCursor.Should().BeNull();
        page.PreviousCursor.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task TakeAsync_Projected_IncludeCount_ReturnsTotal()
    {
        var def = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));

        var page = await _db.MainModels.Keyset(def).IncludeCount()
            .TakeAsync(10, x => new ItemDto(x.Id, x.String, x.Created));

        page.TotalCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task TakeAsync_Projected_EmptyResult_BothCursorsNull()
    {
        var def = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));

        var page = await _db.MainModels.Where(x => x.Id < 0).Keyset(def)
            .TakeAsync(10, x => new ItemDto(x.Id, x.String, x.Created));

        page.Items.Should().BeEmpty();
        page.NextCursor.Should().BeNull();
        page.PreviousCursor.Should().BeNull();
    }

    [Fact]
    public async Task TakeAsync_Projected_NullSelector_Throws()
    {
        var def = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));

        var act = () => _db.MainModels.Keyset(def)
            .TakeAsync<ItemDto>(10, null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task TakeAsync_Projected_NegativePageSize_Throws()
    {
        var def = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));

        var act = () => _db.MainModels.Keyset(def)
            .TakeAsync(0, x => new ItemDto(x.Id, x.String, x.Created));

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task StreamAsync_Projected_YieldsAllItemsInOrder()
    {
        var def = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));

        var collected = new List<ItemDto>();
        await foreach (var batch in _db.MainModels.Keyset(def)
            .StreamAsync(20, x => new ItemDto(x.Id, x.String, x.Created)))
        {
            collected.AddRange(batch);
        }

        collected.Should().HaveCount(99);
        collected.Select(i => i.EntityId).Should().BeInAscendingOrder();
        collected.Select(i => i.EntityId).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task StreamAsync_Projected_BackwardThrows()
    {
        var def = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));

        var act = async () =>
        {
            await foreach (var _ in _db.MainModels.Keyset(def).Before("dummy")
                .StreamAsync(10, x => new ItemDto(x.Id, x.String, x.Created)))
            {
            }
        };

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
