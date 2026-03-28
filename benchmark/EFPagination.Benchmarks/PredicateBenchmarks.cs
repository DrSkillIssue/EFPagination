using System.Linq.Expressions;
using BenchmarkDotNet.Attributes;

namespace EFPagination.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
public class PredicateBenchmarks
{
    private PaginationQueryDefinition<BenchmarkEntity> _singleCol = null!;
    private PaginationQueryDefinition<BenchmarkEntity> _multiCol = null!;
    private IQueryable<BenchmarkEntity> _query = null!;
    private BenchmarkDbContext _db = null!;

    [GlobalSetup]
    public void Setup()
    {
        _singleCol = PaginationQuery.Build<BenchmarkEntity>(b => b.Ascending(x => x.Id));
        _multiCol = PaginationQuery.Build<BenchmarkEntity>(b => b.Descending(x => x.Created).Ascending(x => x.Id));
        _db = BenchmarkDb.Create();
        _query = _db.Items;
    }

    [Benchmark(Baseline = true)]
    public PaginationContext<BenchmarkEntity> Paginate_SingleCol()
        => _query.Paginate(_singleCol, PaginationDirection.Forward, new { Id = 500 });

    [Benchmark]
    public PaginationContext<BenchmarkEntity> Paginate_MultiCol()
        => _query.Paginate(_multiCol, PaginationDirection.Forward, new { Created = DateTime.UtcNow, Id = 500 });

    [Benchmark]
    public PaginationContext<BenchmarkEntity> Paginate_Backward()
        => _query.Paginate(_singleCol, PaginationDirection.Backward, new { Id = 500 });

    [Benchmark]
    public PaginationContext<BenchmarkEntity> Paginate_ColumnValues()
        => _query.Paginate(_singleCol, PaginationDirection.Forward, [new ColumnValue("Id", 500)]);

    [Benchmark]
    public PaginationContext<BenchmarkEntity> Paginate_FirstPage()
        => _query.Paginate(_singleCol);

    [Benchmark]
#pragma warning disable CA1822
    public Expression<Func<BenchmarkEntity, bool>> Paginate_EFCore_ManualWhere()
    {
        var id = 500;
        Expression<Func<BenchmarkEntity, bool>> predicate = e => e.Id > id;
        return predicate;
    }
}
