using System.Buffers.Text;
using System.Text;
using FluentAssertions;
using EFPagination.TestModels;
using Xunit;

namespace EFPagination;

public class EnumAllowlistTests
{
    [Fact]
    public void RegisteredEnum_RoundTrips_Successfully()
    {
        var def = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.EnumValue).Ascending(x => x.Id));

        var cursor = PaginationCursor.Encode([
            new ColumnValue("EnumValue", TestEnum.Value2),
            new ColumnValue("Id", 5)
        ]);

        var success = PaginationCursor.TryDecode(cursor, def, out var values, out var written);

        success.Should().BeTrue();
        written.Should().Be(2);
    }

    [Fact]
    public void UnregisteredEnum_FailsDecode()
    {
        var stableName = $"{typeof(DayOfWeek).FullName}, {typeof(DayOfWeek).Assembly.GetName().Name}";
        var json = $$"""{"v":[[21,"{{stableName}}",3]]}""";
        var utf8 = Encoding.UTF8.GetBytes(json);
        var encoded = string.Create(Base64Url.GetEncodedLength(utf8.Length), utf8, static (span, state) =>
        {
            Base64Url.TryEncodeToChars(state, span, out _);
        });

        var values = new ColumnValue[] { new("Day", null) };
        var success = PaginationCursor.TryDecode(encoded, values, out _);

        success.Should().BeFalse();
    }
}
