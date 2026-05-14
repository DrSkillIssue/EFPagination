using EFPagination.TestModels;
using FluentAssertions;
using Xunit;

namespace EFPagination;

public class SchemaFingerprintTests
{
    [Fact]
    public void TryDecode_WithMatchingFingerprint_Succeeds()
    {
        var def = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));
        var cursor = PaginationCursor.Encode(def, new MainModel { Id = 10 });

        var success = PaginationCursor.TryDecode(cursor, def, out var values, out var metadata);

        success.Should().BeTrue();
        metadata.ValueCount.Should().Be(1);
        metadata.Fingerprint.Should().Be(def.SchemaFingerprint);
    }

    [Fact]
    public void TryDecode_WithMismatchedFingerprint_ReturnsFalse()
    {
        var defV1 = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));
        var cursor = PaginationCursor.Encode(defV1, new MainModel { Id = 10 });

        var defV2 = PaginationQuery.Build<MainModel>(b => b.Descending(x => x.Created).Ascending(x => x.Id));
        PaginationCursor.TryDecode(cursor, defV2, out _, out _).Should().BeFalse();
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
