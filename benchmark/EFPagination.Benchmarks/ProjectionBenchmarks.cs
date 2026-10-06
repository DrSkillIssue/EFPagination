using BenchmarkDotNet.Attributes;

namespace EFPagination.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
public class ProjectionBenchmarks
{
    private PaginationQueryDefinition<BenchmarkEntity> _definition = null!;
    private BenchmarkDbContext _db = null!;

    [GlobalSetup]
    public void Setup()
    {
        _definition = PaginationQuery.Build<BenchmarkEntity>(b => b.Ascending(x => x.Id));
        _db = BenchmarkDb.Create();
    }

    [Benchmark]
    public Task<CursorPage<ProjectedItem>> TakeAsync_Projected_FirstPage()
        => _db.Items.Keyset(_definition).TakeAsync(20, x => new ProjectedItem(x.Id, x.Name));

    public sealed record ProjectedItem(int Id, string Name);
}
