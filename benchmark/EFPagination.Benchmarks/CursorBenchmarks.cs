using BenchmarkDotNet.Attributes;

namespace EFPagination.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
public class CursorBenchmarks
{
    private ColumnValue[] _values = null!;
    private string _encodedTagged = null!;
    private string _encodedSchemaBound = null!;
    private BenchmarkEntity _entity = null!;
    private PaginationQueryDefinition<BenchmarkEntity> _definition = null!;
    private PaginationCursorOptions _schemaBoundOptions;

    [GlobalSetup]
    public void Setup()
    {
        _definition = PaginationQuery.Build<BenchmarkEntity>(b => b.Ascending(x => x.Created).Ascending(x => x.Id));
        _entity = new BenchmarkEntity { Id = 42, Name = "Item 42", Created = DateTime.UtcNow };
        _values = [new("Created", _entity.Created), new("Id", _entity.Id)];
        _schemaBoundOptions = new PaginationCursorOptions(SchemaFingerprint: _definition.SchemaFingerprint);
        _encodedTagged = PaginationCursor.Encode(_values);
        _encodedSchemaBound = PaginationCursor.Encode(_definition, _entity, _schemaBoundOptions);
    }

    [Benchmark(Baseline = true)]
    public string EncodeTaggedFromColumnValues() => PaginationCursor.Encode(_values);

    [Benchmark]
    public string EncodeTaggedWithFingerprint() =>
        PaginationCursor.Encode(_values, _schemaBoundOptions);

    [Benchmark]
    public string EncodeSchemaBoundFromEntity() =>
        PaginationCursor.Encode(_definition, _entity, _schemaBoundOptions);

    [Benchmark]
    public bool DecodeTaggedIntoColumnValues()
    {
        Span<ColumnValue> buf = [new("Created", null), new("Id", null)];
        return PaginationCursor.TryDecode(_encodedTagged, buf, out _);
    }

    [Benchmark]
    public bool DecodeWithDefinition_FromTagged() =>
        PaginationCursor.TryDecode(_encodedTagged, _definition, out _, out _);

    [Benchmark]
    public bool DecodeWithDefinition_FromSchemaBound() =>
        PaginationCursor.TryDecode(_encodedSchemaBound, _definition, out _, out _);
}
