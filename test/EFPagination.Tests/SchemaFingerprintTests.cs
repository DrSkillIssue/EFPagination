using FluentAssertions;
using EFPagination.TestModels;
using Xunit;

namespace EFPagination;

public class SchemaFingerprintTests
{
    [Fact]
    public void TryDecode_WithMatchingFingerprint_Succeeds()
    {
        var def = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));
        var cursor = PaginationCursor.Encode(
            [new ColumnValue("Id", 10)],
            new PaginationCursorOptions(SchemaFingerprint: def.SchemaFingerprint));

        var success = PaginationCursor.TryDecode(cursor, def, out var values, out var written);

        success.Should().BeTrue();
        written.Should().Be(1);
    }

    [Fact]
    public void TryDecode_WithMismatchedFingerprint_ReturnsFalse()
    {
        var defV1 = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));
        var cursor = PaginationCursor.Encode(
            [new ColumnValue("Id", 10)],
            new PaginationCursorOptions(SchemaFingerprint: defV1.SchemaFingerprint));

        var defV2 = PaginationQuery.Build<MainModel>(b => b.Descending(x => x.Created).Ascending(x => x.Id));

        var success = PaginationCursor.TryDecode(cursor, defV2, out _, out _);

        success.Should().BeFalse();
    }

    [Fact]
    public void TryDecode_WithoutFingerprint_SkipsValidation()
    {
        var cursor = PaginationCursor.Encode([new ColumnValue("Id", 10)]);

        var def = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));
        var success = PaginationCursor.TryDecode(cursor, def, out _, out _);

        success.Should().BeTrue();
    }

    [Fact]
    public void Fingerprint_SameDefinition_ProducesSameHash()
    {
        var def1 = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));
        var def2 = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));

        def1.SchemaFingerprint.Should().Be(def2.SchemaFingerprint);
    }

    [Fact]
    public void Fingerprint_DifferentColumns_ProducesDifferentHash()
    {
        var def1 = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));
        var def2 = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Created));

        def1.SchemaFingerprint.Should().NotBe(def2.SchemaFingerprint);
    }

    [Fact]
    public void Fingerprint_DifferentDirection_ProducesDifferentHash()
    {
        var def1 = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));
        var def2 = PaginationQuery.Build<MainModel>(b => b.Descending(x => x.Id));

        def1.SchemaFingerprint.Should().NotBe(def2.SchemaFingerprint);
    }

    [Fact]
    public void Fingerprint_DifferentColumnCount_ProducesDifferentHash()
    {
        var def1 = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));
        var def2 = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id).Ascending(x => x.Created));

        def1.SchemaFingerprint.Should().NotBe(def2.SchemaFingerprint);
    }
}
