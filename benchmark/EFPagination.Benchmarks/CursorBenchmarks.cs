using BenchmarkDotNet.Attributes;

namespace EFPagination.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
public class CursorBenchmarks
{
    private string _encoded = null!;
    private BenchmarkEntity _entity = null!;
    private PaginationQueryDefinition<BenchmarkEntity> _definition = null!;
    private PaginationCursorOptions _options;

    [GlobalSetup]
    public void Setup()
    {
        _definition = PaginationQuery.Build<BenchmarkEntity>(b => b.Ascending(x => x.Created).Ascending(x => x.Id));
        _entity = new BenchmarkEntity { Id = 42, Name = "Item 42", Created = DateTime.UtcNow };
        _options = default;
        _encoded = PaginationCursor.Encode(_definition, _entity, _options);
    }

    [Benchmark(Baseline = true)]
    public string EncodeFromEntity() => PaginationCursor.Encode(_definition, _entity, _options);

    [Benchmark]
    public string EncodeFromEntity_WithTotalCount() =>
        PaginationCursor.Encode(_definition, _entity, _options with { TotalCount = 1234 });

    [Benchmark]
    public bool DecodeWithDefinition() =>
        PaginationCursor.TryDecode(_encoded, _definition, out _, out _);
}
