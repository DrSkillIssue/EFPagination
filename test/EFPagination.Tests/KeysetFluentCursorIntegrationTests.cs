#nullable enable
using EFPagination.TestModels;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EFPagination;

[Collection(SqliteDatabaseCollection.Name)]
public class KeysetFluentCursorIntegrationTests
{
    private readonly TestDbContext _db;

    public KeysetFluentCursorIntegrationTests(SqliteDatabaseFixture fixture)
    {
        var provider = fixture.BuildServices();
        _db = provider.GetService<TestDbContext>()!;
    }

    [Fact]
    public async Task TakeAsync_BackwardToFirstPage_HasNoPreviousCursor_AndHasNextCursor()
    {
        var def = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));

        var first = await _db.MainModels.Keyset(def).TakeAsync(10);
        var second = await _db.MainModels.Keyset(def).After(first.NextCursor!).TakeAsync(10);

        var back = await _db.MainModels.Keyset(def).Before(second.PreviousCursor!).TakeAsync(10);

        back.Items.Select(x => x.Id).Should().BeEquivalentTo(Enumerable.Range(1, 10), o => o.WithStrictOrdering());
        back.PreviousCursor.Should().BeNull();
        back.NextCursor.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task TakeAsync_BackwardToMiddlePage_HasBothCursors()
    {
        var def = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));

        var first = await _db.MainModels.Keyset(def).TakeAsync(10);
        var second = await _db.MainModels.Keyset(def).After(first.NextCursor!).TakeAsync(10);
        var third = await _db.MainModels.Keyset(def).After(second.NextCursor!).TakeAsync(10);

        var back = await _db.MainModels.Keyset(def).Before(third.PreviousCursor!).TakeAsync(10);

        back.Items.Select(x => x.Id).Should().BeEquivalentTo(Enumerable.Range(11, 10), o => o.WithStrictOrdering());
        back.PreviousCursor.Should().NotBeNullOrEmpty();
        back.NextCursor.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task TakeAsync_BackwardToFirstPage_WithDescendingMultiColumnDefinition_HasNoPreviousCursor_AndHasNextCursor()
    {
        var def = PaginationQuery.Build<MainModel>(b => b
            .Descending(x => x.Created)
            .Descending(x => x.Id));

        var first = await _db.MainModels.Keyset(def).TakeAsync(10);
        var second = await _db.MainModels.Keyset(def).After(first.NextCursor!).TakeAsync(10);

        var back = await _db.MainModels.Keyset(def).Before(second.PreviousCursor!).TakeAsync(10);

        back.Items.Select(x => x.Id).Should().BeEquivalentTo(Enumerable.Range(90, 10).Reverse(), o => o.WithStrictOrdering());
        back.PreviousCursor.Should().BeNull();
        back.NextCursor.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task TakeAsync_BeforeBoundValues_HasPreviousCursor_AndHasNextCursor()
    {
        var def = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));
        var values = PaginationValues<MainModel>.Create(def, 31);

        var page = await _db.MainModels.Keyset(def).Before(values).TakeAsync(10);

        page.Items.Select(x => x.Id).Should().BeEquivalentTo(Enumerable.Range(21, 10), o => o.WithStrictOrdering());
        page.PreviousCursor.Should().NotBeNullOrEmpty();
        page.NextCursor.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task TakeAsync_BeforeEntity_HasPreviousCursor_AndHasNextCursor()
    {
        var def = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));
        var reference = await _db.MainModels.SingleAsync(x => x.Id == 31);

        var page = await _db.MainModels.Keyset(def).BeforeEntity(reference).TakeAsync(10);

        page.Items.Select(x => x.Id).Should().BeEquivalentTo(Enumerable.Range(21, 10), o => o.WithStrictOrdering());
        page.PreviousCursor.Should().NotBeNullOrEmpty();
        page.NextCursor.Should().NotBeNullOrEmpty();
    }
}
