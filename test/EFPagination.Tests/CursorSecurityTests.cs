using System.Security.Cryptography;
using EFPagination.TestModels;
using FluentAssertions;
using Xunit;

namespace EFPagination;

public class CursorSecurityTests
{
    private static PaginationQueryDefinition<MainModel> BuildDef() =>
        PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));

    [Fact]
    public void Encode_WithSigningKey_Decode_WithSameKey_Succeeds()
    {
        var def = BuildDef();
        var key = RandomNumberGenerator.GetBytes(32);
        var cursor = PaginationCursor.Encode(def, new MainModel { Id = 42 },
            new PaginationCursorOptions(SigningKey: key));

        var success = PaginationCursor.TryDecode(cursor, def, out var values, out var metadata, signingKey: key);

        success.Should().BeTrue();
        metadata.ValueCount.Should().Be(1);
        values.Count.Should().Be(1);
    }

    [Fact]
    public void Encode_WithSigningKey_Decode_WithWrongKey_Fails()
    {
        var def = BuildDef();
        var key = RandomNumberGenerator.GetBytes(32);
        var wrongKey = RandomNumberGenerator.GetBytes(32);
        var cursor = PaginationCursor.Encode(def, new MainModel { Id = 42 },
            new PaginationCursorOptions(SigningKey: key));

        PaginationCursor.TryDecode(cursor, def, out _, out _, signingKey: wrongKey).Should().BeFalse();
    }

    [Fact]
    public void Encode_WithSigningKey_TamperedPayload_Fails()
    {
        var def = BuildDef();
        var key = RandomNumberGenerator.GetBytes(32);
        var cursor = PaginationCursor.Encode(def, new MainModel { Id = 42 },
            new PaginationCursorOptions(SigningKey: key));

        var chars = cursor.ToCharArray();
        chars[5] = chars[5] == 'A' ? 'B' : 'A';
        var tampered = new string(chars);

        PaginationCursor.TryDecode(tampered, def, out _, out _, signingKey: key).Should().BeFalse();
    }

    [Fact]
    public void Encode_WithoutSigningKey_Decode_WithoutKey_Succeeds()
    {
        var def = BuildDef();
        var cursor = PaginationCursor.Encode(def, new MainModel { Id = 42 });

        var success = PaginationCursor.TryDecode(cursor, def, out var values, out var metadata);

        success.Should().BeTrue();
        metadata.ValueCount.Should().Be(1);
        values.Count.Should().Be(1);
    }

    [Fact]
    public void Encode_WithoutSigningKey_Decode_WithKey_Fails()
    {
        var def = BuildDef();
        var key = RandomNumberGenerator.GetBytes(32);
        var cursor = PaginationCursor.Encode(def, new MainModel { Id = 42 });

        PaginationCursor.TryDecode(cursor, def, out _, out _, signingKey: key).Should().BeFalse();
    }
}
