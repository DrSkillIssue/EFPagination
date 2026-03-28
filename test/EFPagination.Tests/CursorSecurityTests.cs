using System.Security.Cryptography;
using FluentAssertions;
using Xunit;

namespace EFPagination;

public class CursorSecurityTests
{
    [Fact]
    public void Encode_WithSigningKey_Decode_WithSameKey_Succeeds()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var cursor = PaginationCursor.Encode(
            [new ColumnValue("Id", 42), new ColumnValue("Name", "test")],
            new PaginationCursorOptions(SigningKey: key));

        var values = new ColumnValue[] { new("Id", null), new("Name", null) };
        var success = PaginationCursor.TryDecode(cursor, values, out var written, signingKey: key);

        success.Should().BeTrue();
        written.Should().Be(2);
        values[0].Value.Should().Be(42);
        values[1].Value.Should().Be("test");
    }

    [Fact]
    public void Encode_WithSigningKey_Decode_WithWrongKey_Fails()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var wrongKey = RandomNumberGenerator.GetBytes(32);
        var cursor = PaginationCursor.Encode(
            [new ColumnValue("Id", 42)],
            new PaginationCursorOptions(SigningKey: key));

        var values = new ColumnValue[] { new("Id", null) };
        var success = PaginationCursor.TryDecode(cursor, values, out _, signingKey: wrongKey);

        success.Should().BeFalse();
    }

    [Fact]
    public void Encode_WithSigningKey_TamperedPayload_Fails()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var cursor = PaginationCursor.Encode(
            [new ColumnValue("Id", 42)],
            new PaginationCursorOptions(SigningKey: key));

        var chars = cursor.ToCharArray();
        chars[5] = chars[5] == 'A' ? 'B' : 'A';
        var tampered = new string(chars);

        var values = new ColumnValue[] { new("Id", null) };
        var success = PaginationCursor.TryDecode(tampered, values, out _, signingKey: key);

        success.Should().BeFalse();
    }

    [Fact]
    public void Encode_WithoutSigningKey_Decode_WithoutKey_Succeeds()
    {
        var cursor = PaginationCursor.Encode([new ColumnValue("Id", 42)]);

        var values = new ColumnValue[] { new("Id", null) };
        var success = PaginationCursor.TryDecode(cursor, values, out var written);

        success.Should().BeTrue();
        written.Should().Be(1);
    }

    [Fact]
    public void Encode_WithoutSigningKey_Decode_WithKey_Fails()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var cursor = PaginationCursor.Encode([new ColumnValue("Id", 42)]);

        var values = new ColumnValue[] { new("Id", null) };
        var success = PaginationCursor.TryDecode(cursor, values, out _, signingKey: key);

        success.Should().BeFalse();
    }
}
