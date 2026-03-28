using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace EFPagination.Internal;

/// <summary>
/// Bypasses <c>Expression.Lambda&lt;TDelegate&gt;</c> validation overhead by calling
/// the internal <c>Expression1&lt;TDelegate&gt;</c> constructor directly via a cached delegate.
/// Falls back to the public API if the internal type is unavailable.
/// </summary>
internal static class FastLambda<T>
{
    private static readonly Func<Expression, ParameterExpression, Expression<Func<T, bool>>>? s_factory = BuildFactory();

    private static Func<Expression, ParameterExpression, Expression<Func<T, bool>>>? BuildFactory()
    {
        try
        {
            var expression1Type = typeof(Expression).Assembly
                .GetType("System.Linq.Expressions.Expression1`1")
                ?.MakeGenericType(typeof(Func<T, bool>));

            if (expression1Type is null)
                return null;

            var ctor = expression1Type.GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                [typeof(Expression), typeof(ParameterExpression)]);

            if (ctor is null)
                return null;

            var bodyParam = Expression.Parameter(typeof(Expression), "body");
            var parParam = Expression.Parameter(typeof(ParameterExpression), "par");
            var newExpr = Expression.New(ctor, bodyParam, parParam);
            var castExpr = Expression.Convert(newExpr, typeof(Expression<Func<T, bool>>));

            return Expression.Lambda<Func<Expression, ParameterExpression, Expression<Func<T, bool>>>>(
                castExpr, bodyParam, parParam).Compile();
        }
        catch
        {
            return null;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Expression<Func<T, bool>> Create(Expression body, ParameterExpression parameter)
    {
        return s_factory is not null
            ? s_factory(body, parameter)
            : Expression.Lambda<Func<T, bool>>(body, parameter);
    }
}
