using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EFPagination.TestModels;
using Xunit;

namespace EFPagination;

[Collection(SqliteDatabaseCollection.Name)]
public class ByteArrayKeysetTests
{
    private readonly TestDbContext _dbContext;

    public ByteArrayKeysetTests(SqliteDatabaseFixture fixture)
    {
        var provider = fixture.BuildServices();
        _dbContext = provider.GetService<TestDbContext>();
    }

    [Fact]
    public void Cursor_RoundTrips_ByteArray()
    {
        var definition = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Bytes).Ascending(x => x.Id));
        var entity = new MainModel { Id = 42, Bytes = [0x01, 0x02, 0x03, 0xFF] };

        var encoded = PaginationCursor.Encode(definition, entity);
        var success = PaginationCursor.TryDecode(encoded, definition, out var values, out var metadata);

        success.Should().BeTrue();
        metadata.ValueCount.Should().Be(2);
        values.Count.Should().Be(2);
    }

    [Fact]
    public async Task Keyset_Paginates_By_ByteArray()
    {
        var definition = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Bytes).Ascending(x => x.Id));

        var firstPage = await _dbContext.MainModels.Keyset(definition).TakeAsync(10);

        firstPage.Items.Select(x => x.Id).Should().BeEquivalentTo(Enumerable.Range(1, 10), o => o.WithStrictOrdering());
        firstPage.NextCursor.Should().NotBeNull();

        var secondPage = await _dbContext.MainModels.Keyset(definition).After(firstPage.NextCursor!).TakeAsync(10);

        secondPage.Items.Select(x => x.Id).Should().BeEquivalentTo(Enumerable.Range(11, 10), o => o.WithStrictOrdering());
    }
}
