using System.Linq.Expressions;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using EFPagination.Internal;

namespace EFPagination.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
public class ExpressionMicroBenchmarks
{
    private static readonly FieldInfo s_valueField = typeof(ValueHolder).GetField("Value")!;
    private static readonly ParameterExpression s_entityParam = Expression.Parameter(typeof(BenchmarkEntity), "entity");
    private static readonly MemberExpression s_idAccess = Expression.Property(s_entityParam, nameof(BenchmarkEntity.Id));

    private ValueHolder _holder = null!;
    private ConstantExpression _constant = null!;
    private MemberExpression _fieldAccess = null!;
    private UnaryExpression _convert = null!;
    private BinaryExpression _gt = null!;

    [GlobalSetup]
    public void Setup()
    {
        _holder = new ValueHolder { Value = 500 };
        _constant = Expression.Constant(_holder);
        _fieldAccess = Expression.Field(_constant, s_valueField);
        _convert = Expression.Convert(_fieldAccess, typeof(int));
        _gt = Expression.GreaterThan(s_idAccess, _convert);
    }

    [Benchmark]
    public object AllocValueHolder() => new ValueHolder { Value = 500 };

    [Benchmark]
    public ConstantExpression AllocConstant() => Expression.Constant(new ValueHolder { Value = 500 });

    [Benchmark]
    public MemberExpression AllocField() => Expression.Field(_constant, s_valueField);

    [Benchmark]
    public UnaryExpression AllocConvert() => Expression.Convert(_fieldAccess, typeof(int));

    [Benchmark]
    public BinaryExpression AllocGT() => Expression.GreaterThan(s_idAccess, _convert);

    [Benchmark]
    public Expression<Func<BenchmarkEntity, bool>> AllocLambda()
        => Expression.Lambda<Func<BenchmarkEntity, bool>>(_gt, s_entityParam);

    [Benchmark]
    public Expression<Func<BenchmarkEntity, bool>> FullChain()
    {
        var holder = new ValueHolder { Value = 500 };
        var constant = Expression.Constant(holder);
        var field = Expression.Field(constant, s_valueField);
        var convert = Expression.Convert(field, typeof(int));
        var gt = Expression.GreaterThan(s_idAccess, convert);
        return Expression.Lambda<Func<BenchmarkEntity, bool>>(gt, s_entityParam);
    }
}
