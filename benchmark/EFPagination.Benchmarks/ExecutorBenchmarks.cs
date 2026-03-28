using BenchmarkDotNet.Attributes;
using Microsoft.EntityFrameworkCore;

namespace EFPagination.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
public class ExecutorBenchmarks
{
    private PaginationQueryDefinition<BenchmarkEntity> _definition = null!;
    private BenchmarkDbContext _db = null!;
    private string _cursorPage2 = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        _definition = PaginationQuery.Build<BenchmarkEntity>(b => b.Ascending(x => x.Id));
        _db = BenchmarkDb.Create();

        var p1 = await PaginationExecutor.ExecuteFromCursorAsync(
            _db.Items, _definition, new ExecutionOptions(PageSize: 20), cursor: ReadOnlySpan<char>.Empty);
        _cursorPage2 = p1.NextCursor!;
    }

    [Benchmark(Baseline = true)]
    public Task<KeysetPage<BenchmarkEntity>> ExecuteAsync_FirstPage()
        => PaginationExecutor.ExecuteAsync(_db.Items, _definition, new ExecutionOptions(PageSize: 20));

    [Benchmark]
    public Task<KeysetPage<BenchmarkEntity>> ExecuteAsync_WithReference()
        => PaginationExecutor.ExecuteAsync(_db.Items, _definition, new ExecutionOptions(PageSize: 20), new { Id = 500 });

    [Benchmark]
    public Task<CursorPage<BenchmarkEntity>> ExecuteFromCursor_FirstPage()
        => PaginationExecutor.ExecuteFromCursorAsync(_db.Items, _definition, new ExecutionOptions(PageSize: 20), cursor: ReadOnlySpan<char>.Empty);

    [Benchmark]
    public Task<CursorPage<BenchmarkEntity>> ExecuteFromCursor_Page2()
        => PaginationExecutor.ExecuteFromCursorAsync(_db.Items, _definition, new ExecutionOptions(PageSize: 20), _cursorPage2);

    [Benchmark]
    public Task<KeysetPage<BenchmarkEntity>> MaterializeAsync_FirstPage()
    {
        var ctx = _db.Items.Paginate(_definition);
        return ctx.MaterializeAsync(20);
    }

    [Benchmark]
    public Task<KeysetPage<BenchmarkEntity>> MaterializeAsync_WithRef()
    {
        var ctx = _db.Items.Paginate(_definition, PaginationDirection.Forward, new { Id = 500 });
        return ctx.MaterializeAsync(20);
    }

    [Benchmark]
    public async Task<List<BenchmarkEntity>> Manual_Skip_Take()
        => await _db.Items.OrderBy(x => x.Id).Where(x => x.Id > 500).Take(20).ToListAsync();

    [Benchmark]
    public async Task<List<BenchmarkEntity>> Manual_Offset()
        => await _db.Items.OrderBy(x => x.Id).Skip(500).Take(20).ToListAsync();
}
