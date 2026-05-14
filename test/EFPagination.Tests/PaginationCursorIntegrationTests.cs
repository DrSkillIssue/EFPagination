using System.Buffers.Text;
using System.Globalization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EFPagination.TestModels;
using Xunit;

namespace EFPagination;

[Collection(SqliteDatabaseCollection.Name)]
public class PaginationCursorIntegrationTests
{
    private readonly TestDbContext _dbContext;

    public PaginationCursorIntegrationTests(SqliteDatabaseFixture fixture)
    {
        var provider = fixture.BuildServices();
        _dbContext = provider.GetService<TestDbContext>();
    }

    [Fact]
    public void EncodeDecode_RoundTrips_AllPrimitiveTypes_AndMetadata()
    {
        var definition = PaginationQuery.Build<AllPrimitivesModel>(b => b
            .Ascending(x => x.String)
            .Ascending(x => x.Boolean)
            .Ascending(x => x.Char)
            .Ascending(x => x.Byte)
            .Ascending(x => x.SByte)
            .Ascending(x => x.Int16)
            .Ascending(x => x.UInt16)
            .Ascending(x => x.Int32)
            .Ascending(x => x.UInt32)
            .Ascending(x => x.Int64)
            .Ascending(x => x.UInt64)
            .Ascending(x => x.Single)
            .Ascending(x => x.Double)
            .Ascending(x => x.Decimal)
            .Ascending(x => x.Guid)
            .Ascending(x => x.DateTime)
            .Ascending(x => x.DateTimeOffset)
            .Ascending(x => x.DateOnly)
            .Ascending(x => x.TimeOnly)
            .Ascending(x => x.TimeSpan)
            .Ascending(x => x.Enum));

        var entity = new AllPrimitivesModel
        {
            String = "hello",
            Boolean = true,
            Char = 'Z',
            Byte = 9,
            SByte = -10,
            Int16 = 7,
            UInt16 = 8,
            Int32 = 42,
            UInt32 = 42U,
            Int64 = 42L,
            UInt64 = 42UL,
            Single = 1.25f,
            Double = 2.5d,
            Decimal = 3.75m,
            Guid = Guid.Parse("b0a8f446-a2dd-4f79-9ab8-0c0ae43854d7"),
            DateTime = new DateTime(2024, 05, 06, 07, 08, 09, DateTimeKind.Utc),
            DateTimeOffset = new DateTimeOffset(2024, 05, 06, 07, 08, 09, TimeSpan.FromHours(2)),
            DateOnly = new DateOnly(2024, 05, 06),
            TimeOnly = new TimeOnly(12, 34, 56, 789),
            TimeSpan = TimeSpan.FromHours(6) + TimeSpan.FromMinutes(7) + TimeSpan.FromSeconds(8),
            Enum = TestEnum.Value2,
        };

        var encoded = PaginationCursor.Encode(definition, entity, new PaginationCursorOptions("created", 99));

        var success = PaginationCursor.TryDecode(encoded, definition, out var values, out var metadata);

        success.Should().BeTrue();
        metadata.ValueCount.Should().Be(21);
        metadata.SortBy.Should().Be("created");
        metadata.TotalCount.Should().Be(99);
        values.Count.Should().Be(21);
    }

    [Fact]
    public void TryDecode_ReturnsFalse_ForMalformedPayloads()
    {
        var definition = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));

        PaginationCursor.TryDecode("", definition, out _, out _).Should().BeFalse();
        PaginationCursor.TryDecode("%%%", definition, out _, out _).Should().BeFalse();
        PaginationCursor.TryDecode(EncodeRawBytes([0x00]), definition, out _, out _).Should().BeFalse();  // bad version
        PaginationCursor.TryDecode(EncodeRawBytes([0x04, 0xFF]), definition, out _, out _).Should().BeFalse();  // truncated
    }

    [Fact]
    public void EncodeDecode_RoundTrips_TimeSpan_WithLargeDayComponent()
    {
        var definition = PaginationQuery.Build<AllPrimitivesModel>(b => b.Ascending(x => x.TimeSpan));
        var duration = TimeSpan.ParseExact("-1234567.00:02:00", "c", CultureInfo.InvariantCulture);
        var entity = new AllPrimitivesModel { TimeSpan = duration };

        var encoded = PaginationCursor.Encode(definition, entity);

        var success = PaginationCursor.TryDecode(encoded, definition, out var values, out var metadata);

        success.Should().BeTrue();
        metadata.ValueCount.Should().Be(1);
        values.Count.Should().Be(1);
    }

    [Fact]
    public async Task Cursor_RoundTrips_ThroughPaginationFlow_WithMetadata()
    {
        var definition = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Created).Ascending(x => x.Id));

        var firstPage = await PaginationExecutor.ExecuteAsync(_dbContext.MainModels, definition, new ExecutionOptions(PageSize: 10, IncludeCount: true));
        var lastItem = firstPage.Items[^1];
        var cursor = PaginationCursor.Encode(definition, lastItem,
            new PaginationCursorOptions("created", firstPage.TotalCount));

        var success = PaginationCursor.TryDecode(cursor, definition, out var decoded, out var metadata);

        success.Should().BeTrue();
        metadata.ValueCount.Should().Be(2);
        metadata.SortBy.Should().Be("created");
        metadata.TotalCount.Should().Be(firstPage.TotalCount);

        var secondPage = await _dbContext.MainModels
            .Paginate(definition, PaginationDirection.Forward, decoded).Query
            .Take(10)
            .ToListAsync();

        secondPage.Select(x => x.Id).Should().BeEquivalentTo(Enumerable.Range(11, 10), options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task TryDecode_ReturnsFalse_ForTamperedPaginationCursor()
    {
        var definition = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Created).Ascending(x => x.Id));
        var firstPage = await PaginationExecutor.ExecuteAsync(_dbContext.MainModels, definition, new ExecutionOptions(PageSize: 10, IncludeCount: true));
        var lastItem = firstPage.Items[^1];

        var validCursor = PaginationCursor.Encode(definition, lastItem,
            new PaginationCursorOptions("created", firstPage.TotalCount));

        // Flip a byte in the encoded base64 payload to corrupt the body / fingerprint.
        var chars = validCursor.ToCharArray();
        chars[8] = chars[8] == 'A' ? 'B' : 'A';
        var tamperedCursor = new string(chars);

        PaginationCursor.TryDecode(tamperedCursor, definition, out _, out _).Should().BeFalse();
    }

    [Fact]
    public async Task TryDecode_DefinitionOverload_Feeds_Executor_Directly()
    {
        var definition = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Created).Ascending(x => x.Id));
        var firstPage = await PaginationExecutor.ExecuteAsync(_dbContext.MainModels, definition, new ExecutionOptions(PageSize: 10, IncludeCount: true));
        var lastItem = firstPage.Items[^1];
        var cursor = PaginationCursor.Encode(definition, lastItem);

        var success = PaginationCursor.TryDecode(cursor, definition, out var values, out var metadata);

        success.Should().BeTrue();
        metadata.ValueCount.Should().Be(2);

        var page = await PaginationExecutor.ExecuteAsync(_dbContext.MainModels, definition, new ExecutionOptions(PageSize: 10), values);

        page.Items.Select(x => x.Id).Should().BeEquivalentTo(Enumerable.Range(11, 10), options => options.WithStrictOrdering());
        page.HasNext.Should().BeTrue();
    }

    [Fact]
    public async Task TryDecode_DefinitionOverload_Supports_ComputedColumns()
    {
        var definition = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.CreatedNullable ?? DateTime.MinValue).Ascending(x => x.Id));
        var values = PaginationValues<MainModel>.Create(definition, DateTime.MinValue, 10);
        var cursor = PaginationCursor.Encode(definition, values);

        var success = PaginationCursor.TryDecode(cursor, definition, out var decoded, out var metadata);

        success.Should().BeTrue();
        metadata.ValueCount.Should().Be(2);

        var page = await PaginationExecutor.ExecuteAsync(_dbContext.MainModels, definition, new ExecutionOptions(PageSize: 10), decoded);

        page.Items.Should().NotBeEmpty();
    }

    private static string EncodeRawBytes(byte[] bytes)
    {
        return string.Create(Base64Url.GetEncodedLength(bytes.Length), bytes, static (span, state) =>
        {
            Base64Url.TryEncodeToChars(state, span, out _);
        });
    }
}
