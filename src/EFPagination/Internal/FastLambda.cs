using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace EFPagination.Internal;

/// <summary>
/// Bypasses <see cref="Expression.Lambda{TDelegate}(Expression, ParameterExpression[])"/>
/// validation overhead by calling the internal <c>Expression1&lt;TDelegate&gt;</c> constructor
/// directly via a cached delegate. Falls back to the public API when the internal type is
/// unavailable (for example under linking or in a future BCL version).
/// </summary>
/// <typeparam name="T">The entity type for the single-parameter <c>Func&lt;T, bool&gt;</c> lambdas produced.</typeparam>
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

    /// <summary>
    /// Creates an <see cref="Expression{TDelegate}"/> over a single parameter, skipping the
    /// validation overhead of the public API when possible.
    /// </summary>
    /// <param name="body">The expression body.</param>
    /// <param name="parameter">The single parameter of the lambda.</param>
    /// <returns>A new <see cref="Expression{TDelegate}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Expression<Func<T, bool>> Create(Expression body, ParameterExpression parameter)
    {
        return s_factory is not null
            ? s_factory(body, parameter)
            : Expression.Lambda<Func<T, bool>>(body, parameter);
    }
}
