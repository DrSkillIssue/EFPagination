using BenchmarkDotNet.Attributes;
using Microsoft.EntityFrameworkCore;

namespace EFPagination.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
public class StreamingBenchmarks
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
    public async Task<int> PaginateAllAsync_100PerPage()
    {
        var count = 0;
        await foreach (var page in PaginationStreaming.PaginateAllAsync(_db.Items, _definition, 100))
            count += page.Count;
        return count;
    }

    [Benchmark]
    public async Task<int> Manual_OffsetLoop_100PerPage()
    {
        var count = 0;
        var offset = 0;
        List<BenchmarkEntity> page;
        do
        {
            page = await _db.Items.OrderBy(x => x.Id).Skip(offset).Take(100).ToListAsync();
            count += page.Count;
            offset += page.Count;
        } while (page.Count == 100);
        return count;
    }
}
