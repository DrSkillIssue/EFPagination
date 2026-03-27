using System.Buffers.Text;
using System.Globalization;
using System.Text;
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
    public void EncodeDecode_RoundTrips_TypedValues_AndMetadata()
    {
        DateTime dateTime = new(2024, 05, 06, 07, 08, 09, DateTimeKind.Utc);
        DateTimeOffset dateTimeOffset = new(2024, 05, 06, 07, 08, 09, TimeSpan.FromHours(2));
        DateOnly dateOnly = new(2024, 05, 06);
        TimeOnly timeOnly = new(12, 34, 56, 789);
        Guid guid = Guid.Parse("b0a8f446-a2dd-4f79-9ab8-0c0ae43854d7");

        ColumnValue[] values =
        [
            new("Null", null),
            new("String", "hello"),
            new("Boolean", true),
            new("Char", 'Z'),
            new("Int32", 42),
            new("Int64", 42L),
            new("UInt32", 42U),
            new("UInt64", 42UL),
            new("Int16", (short)7),
            new("UInt16", (ushort)8),
            new("Byte", (byte)9),
            new("SByte", (sbyte)-10),
            new("Single", 1.25f),
            new("Double", 2.5d),
            new("Decimal", 3.75m),
            new("Guid", guid),
            new("DateTime", dateTime),
            new("DateTimeOffset", dateTimeOffset),
            new("DateOnly", dateOnly),
            new("TimeOnly", timeOnly),
            new("TimeSpan", TimeSpan.FromHours(6) + TimeSpan.FromMinutes(7) + TimeSpan.FromSeconds(8)),
            new("Enum", TestEnum.Value2),
        ];

        var encoded = PaginationCursor.Encode(values, new PaginationCursorOptions("created", 99));

        var decoded = values.Select(x => new ColumnValue(x.Name, null)).ToArray();
        var success = PaginationCursor.TryDecode(encoded, decoded, out var written, out var sortBy, out var totalCount);

        success.Should().BeTrue();
        written.Should().Be(values.Length);
        sortBy.Should().Be("created");
        totalCount.Should().Be(99);

        for (var i = 0; i < values.Length; i++)
        {
            decoded[i].Name.Should().Be(values[i].Name);
            decoded[i].Value.Should().Be(values[i].Value);

            if (values[i].Value is not null)
                decoded[i].Value.Should().BeOfType(values[i].Value.GetType());
        }
    }

    [Fact]
    public void TryDecode_ReturnsFalse_ForMalformedPayloads()
    {
        ColumnValue[] values = [new("Id", null)];

        PaginationCursor.TryDecode("", values, out _).Should().BeFalse();
        PaginationCursor.TryDecode("%%%", values, out _).Should().BeFalse();
        PaginationCursor.TryDecode(EncodeJson("[]"), values, out _).Should().BeFalse();
        PaginationCursor.TryDecode(EncodeJson("{\"v\":[[4,\"not-a-number\"]]}"), values, out _).Should().BeFalse();
    }

    [Fact]
    public void EncodeDecode_RoundTrips_TimeSpan_WithLargeDayComponent()
    {
        var duration = TimeSpan.ParseExact("-1234567.00:02:00", "c", CultureInfo.InvariantCulture);
        ColumnValue[] values = [new("Duration", duration)];

        var encoded = PaginationCursor.Encode(values);
        var decoded = new[] { new ColumnValue("Duration", null) };

        var success = PaginationCursor.TryDecode(encoded, decoded, out var written);

        success.Should().BeTrue();
        written.Should().Be(1);
        decoded[0].Value.Should().Be(duration);
        decoded[0].Value.Should().BeOfType<TimeSpan>();
    }

    [Fact]
    public void TryDecode_ReturnsFalse_WhenCursorContainsMoreValuesThanExpected()
    {
        var encoded = PaginationCursor.Encode(
        [
            new ColumnValue("Id", 10),
            new ColumnValue("Created", new DateTime(2024, 01, 01, 0, 0, 0, DateTimeKind.Utc)),
        ]);

        var decoded = new[] { new ColumnValue("Id", null) };

        PaginationCursor.TryDecode(encoded, decoded, out _).Should().BeFalse();
    }

    [Fact]
    public async Task Cursor_RoundTrips_ThroughPaginationFlow_WithMetadata()
    {
        var definition = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Created).Ascending(x => x.Id));

        var firstPage = await PaginationExecutor.ExecuteAsync(_dbContext.MainModels, definition, 10, null, includeCount: true);
        var lastItem = firstPage.Items[^1];
        var cursor = PaginationCursor.Encode(
        [
            new ColumnValue(nameof(MainModel.Created), lastItem.Created),
            new ColumnValue(nameof(MainModel.Id), lastItem.Id),
        ],
        new PaginationCursorOptions("created", firstPage.TotalCount));

        var decoded = new[]
        {
            new ColumnValue(nameof(MainModel.Created), null),
            new ColumnValue(nameof(MainModel.Id), null),
        };

        var success = PaginationCursor.TryDecode(cursor, decoded, out var written, out var sortBy, out var totalCount);

        success.Should().BeTrue();
        written.Should().Be(2);
        sortBy.Should().Be("created");
        totalCount.Should().Be(firstPage.TotalCount);

        var reference = new
        {
            Created = decoded[0].Value.Should().BeOfType<DateTime>().Subject,
            Id = decoded[1].Value.Should().BeOfType<int>().Subject,
        };

        var secondPage = await _dbContext.MainModels
            .PaginateQuery(definition, PaginationDirection.Forward, reference)
            .Take(10)
            .ToListAsync();

        secondPage.Select(x => x.Id).Should().BeEquivalentTo(Enumerable.Range(11, 10), options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task TryDecode_ReturnsFalse_ForTamperedPaginationCursor()
    {
        var definition = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Created).Ascending(x => x.Id));
        var firstPage = await PaginationExecutor.ExecuteAsync(_dbContext.MainModels, definition, 10, null, includeCount: true);
        var lastItem = firstPage.Items[^1];

        var validCursor = PaginationCursor.Encode(
        [
            new ColumnValue(nameof(MainModel.Created), lastItem.Created),
            new ColumnValue(nameof(MainModel.Id), lastItem.Id),
        ],
        new PaginationCursorOptions("created", firstPage.TotalCount));

        var tamperedCursor = TamperCursorJson(validCursor, static json => json.Replace("\"c\":99", "\"c\":99}[]", StringComparison.Ordinal));
        var decoded = new[]
        {
            new ColumnValue(nameof(MainModel.Created), null),
            new ColumnValue(nameof(MainModel.Id), null),
        };

        PaginationCursor.TryDecode(tamperedCursor, decoded, out _, out _, out _).Should().BeFalse();
    }

    private static string EncodeJson(string json)
    {
        var utf8 = Encoding.UTF8.GetBytes(json);
        return string.Create(Base64Url.GetEncodedLength(utf8.Length), utf8, static (span, state) =>
        {
            Base64Url.TryEncodeToChars(state, span, out _);
        });
    }

    private static string TamperCursorJson(string encoded, Func<string, string> mutate)
    {
        Span<byte> buffer = stackalloc byte[Base64Url.GetMaxDecodedLength(encoded.Length)];
        Base64Url.TryDecodeFromChars(encoded, buffer, out var bytesWritten).Should().BeTrue();

        var json = Encoding.UTF8.GetString(buffer[..bytesWritten]);
        return EncodeJson(mutate(json));
    }
}
