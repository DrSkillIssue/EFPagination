using System.Collections.Frozen;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace EFPagination.Internal;

/// <summary>
/// Builds the disjunction-of-conjunctions keyset predicate
/// (<c>(col1 &gt; ref1) OR (col1 = ref1 AND col2 &gt; ref2) OR ...</c>) plus an optional
/// first-column access clause for index-seek friendliness.
/// </summary>
internal static class FilterPredicateStrategy
{
    /// <summary>When <see langword="true"/>, prepends a <c>col1 &gt;= ref1</c> clause.</summary>
    internal const bool EnableFirstColPredicateOpt = true;

    /// <summary>Cached <c>0</c> constant for <c>CompareTo</c> comparisons.</summary>
    internal static ConstantExpression ZeroConstant { get; } = Expression.Constant(0);

    private static readonly FrozenDictionary<Type, MethodInfo> s_typeToCompareToMethod = new Dictionary<Type, MethodInfo>
    {
        { typeof(string), GetCompareToMethod(typeof(string)) },
        { typeof(Guid), GetCompareToMethod(typeof(Guid)) },
        { typeof(bool), GetCompareToMethod(typeof(bool)) },
    }.ToFrozenDictionary();

    /// <summary>Creates a <see cref="CachedPredicateTemplate{T}"/> for the specified columns.</summary>
    public static CachedPredicateTemplate<T> CreateTemplate<T>(PaginationColumn<T>[] columns) => new(columns);

    /// <summary>Builds the predicate body using typed expressions for reference values.</summary>
    public static Expression BuildExpressionCore<T>(
        PaginationColumn<T>[] columns,
        PaginationDirection direction,
        Expression[] referenceValueExpressions,
        ParameterExpression param)
    {
        var memberAccessExpressions = new Expression[columns.Length];
        for (var i = 0; i < columns.Length; i++)
            memberAccessExpressions[i] = columns[i].MakeAccessExpression(param);

        var orExpression = default(BinaryExpression)!;
        var innerLimit = 1;
        for (var i = 0; i < columns.Length; i++)
        {
            var andExpression = default(BinaryExpression)!;

            for (var j = 0; j < innerLimit; j++)
            {
                var isInnerLastOperation = j + 1 == innerLimit;
                var column = columns[j];
                var memberAccess = memberAccessExpressions[j];
                var referenceValueExpression = referenceValueExpressions[j];

                BinaryExpression innerExpression = isInnerLastOperation
                    ? MakeComparisonExpression(column, memberAccess, referenceValueExpression,
                        GetComparisonExpressionToApply(direction, column, orEqual: false))
                    : Expression.Equal(memberAccess, EnsureMatchingType(memberAccess, referenceValueExpression));

                andExpression = andExpression is null ? innerExpression : Expression.And(andExpression, innerExpression);
            }

            orExpression = orExpression is null ? andExpression : Expression.Or(orExpression, andExpression);
            innerLimit++;
        }

        Expression finalExpression = orExpression;

        if (EnableFirstColPredicateOpt && columns.Length > 1)
        {
            var firstColumn = columns[0];
            var compare = GetComparisonExpressionToApply(direction, firstColumn, orEqual: true);
            var accessPredicateClause = MakeComparisonExpression(
                firstColumn, memberAccessExpressions[0], referenceValueExpressions[0], compare);
            finalExpression = Expression.And(accessPredicateClause, finalExpression);
        }

        return finalExpression;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static BinaryExpression MakeComparisonExpression<T>(
        PaginationColumn<T> column,
        Expression memberAccess, Expression referenceValue,
        Func<Expression, Expression, BinaryExpression> compare)
    {
        if (s_typeToCompareToMethod.TryGetValue(column.Type, out var compareToMethod))
        {
            var methodCall = Expression.Call(memberAccess, compareToMethod, EnsureMatchingType(memberAccess, referenceValue));
            return compare(methodCall, ZeroConstant);
        }

        return compare(
            EnsureAdditionalConversions(memberAccess),
            EnsureAdditionalConversions(EnsureMatchingType(memberAccess, referenceValue)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Expression EnsureAdditionalConversions(Expression expression)
    {
        if (expression.Type.IsEnum)
        {
            var underlyingType = Enum.GetUnderlyingType(expression.Type);
            return Expression.Convert(expression, underlyingType);
        }
        return expression;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Expression EnsureMatchingType(Expression memberExpression, Expression targetExpression)
        => memberExpression.Type != targetExpression.Type
            ? Expression.Convert(targetExpression, memberExpression.Type)
            : targetExpression;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Func<Expression, Expression, BinaryExpression> GetComparisonExpressionToApply<T>(
        PaginationDirection direction, PaginationColumn<T> column, bool orEqual)
    {
        var greaterThan = (direction == PaginationDirection.Backward) ^ !column.IsDescending;
        return orEqual
            ? (greaterThan ? Expression.GreaterThanOrEqual : Expression.LessThanOrEqual)
            : (greaterThan ? Expression.GreaterThan : Expression.LessThan);
    }

    private static MethodInfo GetCompareToMethod(Type type)
        => type.GetMethod(nameof(string.CompareTo), [type])
           ?? throw new InvalidOperationException($"Didn't find a CompareTo method on type {type.Name}.");
}
