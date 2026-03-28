using BenchmarkDotNet.Attributes;

namespace EFPagination.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
public class CursorBenchmarks
{
    private ColumnValue[] _values = null!;
    private string _encoded = null!;
    private PaginationQueryDefinition<BenchmarkEntity> _definition = null!;

    [GlobalSetup]
    public void Setup()
    {
        _definition = PaginationQuery.Build<BenchmarkEntity>(b => b.Ascending(x => x.Created).Ascending(x => x.Id));
        _values = [new("Created", DateTime.UtcNow), new("Id", 42)];
        _encoded = PaginationCursor.Encode(_values);
    }

    [Benchmark]
    public string Encode() => PaginationCursor.Encode(_values);

    [Benchmark]
    public bool Decode()
    {
        Span<ColumnValue> buf = [new("Created", null), new("Id", null)];
        return PaginationCursor.TryDecode(_encoded, buf, out _);
    }

    [Benchmark]
    public bool DecodeWithDefinition() => PaginationCursor.TryDecode(_encoded, _definition, out _, out _);

    [Benchmark]
    public string EncodeWithFingerprint() => PaginationCursor.Encode(_values, new PaginationCursorOptions(SchemaFingerprint: _definition.SchemaFingerprint));

    [Benchmark]
    public string RoundTrip()
    {
        PaginationCursor.TryDecode(_encoded, _definition, out var values, out _);
        return PaginationCursor.Encode(_values);
    }
}
