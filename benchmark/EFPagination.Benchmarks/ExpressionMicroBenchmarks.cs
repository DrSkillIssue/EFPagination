using System.Linq.Expressions;
using BenchmarkDotNet.Attributes;
using EFPagination.Internal;

namespace EFPagination.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
public class ExpressionMicroBenchmarks
{
    private static readonly ParameterExpression s_entityParam = Expression.Parameter(typeof(BenchmarkEntity), "entity");
    private static readonly MemberExpression s_idAccess = Expression.Property(s_entityParam, nameof(BenchmarkEntity.Id));

    private ColumnBinding<int> _binding = null!;
    private Expression _fieldAccess = null!;
    private BinaryExpression _gt = null!;

    [GlobalSetup]
    public void Setup()
    {
        _binding = new ColumnBinding<int> { Value = 500 };
        _fieldAccess = _binding.CreateValueAccessExpression();
        _gt = Expression.GreaterThan(s_idAccess, _fieldAccess);
    }

    [Benchmark]
    public object AllocBinding() => new ColumnBinding<int> { Value = 500 };

    [Benchmark]
    public Expression AllocFieldAccess() => _binding.CreateValueAccessExpression();

    [Benchmark]
    public BinaryExpression AllocGT() => Expression.GreaterThan(s_idAccess, _fieldAccess);

    [Benchmark]
    public Expression<Func<BenchmarkEntity, bool>> AllocLambda()
        => Expression.Lambda<Func<BenchmarkEntity, bool>>(_gt, s_entityParam);

    [Benchmark]
    public Expression<Func<BenchmarkEntity, bool>> FullChain()
    {
        var binding = new ColumnBinding<int> { Value = 500 };
        var field = binding.CreateValueAccessExpression();
        var gt = Expression.GreaterThan(s_idAccess, field);
        return Expression.Lambda<Func<BenchmarkEntity, bool>>(gt, s_entityParam);
    }
}
