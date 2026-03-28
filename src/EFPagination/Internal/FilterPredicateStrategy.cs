using System.Collections.Frozen;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace EFPagination.Internal;

/// <summary>
/// Holds a mutable reference value whose field access produces a <see cref="MemberExpression"/>
/// on a <see cref="ConstantExpression"/>. EF Core recognizes this pattern and parameterizes the
/// value in the generated SQL.
/// </summary>
internal sealed class ValueHolder
{
    /// <summary>
    /// The reference value. Mutated per-call; read by EF Core at parameterization time.
    /// </summary>
    public object? Value;

    /// <summary>
    /// Cached <see cref="FieldInfo"/> for <see cref="Value"/>, used to build
    /// <see cref="Expression.Field(Expression?, FieldInfo)"/> nodes without repeated reflection.
    /// </summary>
    internal static readonly FieldInfo ValueField = typeof(ValueHolder).GetField(nameof(Value))!;
}

/// <summary>
/// Builds a filter predicate expression from columns, a direction, and a reference object.
/// </summary>
internal interface IFilterPredicateStrategy
{
    /// <summary>
    /// Builds a complete filter predicate lambda expression.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="columns">The pagination columns defining the sort order.</param>
    /// <param name="direction">The pagination direction.</param>
    /// <param name="reference">The reference object whose property values define the cursor position.</param>
    /// <returns>A lambda expression suitable for use in a LINQ <c>Where</c> clause.</returns>
    Expression<Func<T, bool>> BuildFilterPredicateExpression<T>(
        PaginationColumn<T>[] columns,
        PaginationDirection direction,
        object reference);
}

/// <summary>
/// Exposes the core expression-building logic to <see cref="CachedPredicateTemplate{T}"/>
/// for template construction with placeholder expressions.
/// </summary>
internal interface IFilterPredicateStrategyInternal
{
    /// <summary>
    /// Builds the predicate expression body using placeholder expressions for reference values.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="columns">The pagination columns.</param>
    /// <param name="direction">The pagination direction.</param>
    /// <param name="referenceValueExpressions">Placeholder expressions representing reference values.</param>
    /// <param name="param">The entity parameter expression.</param>
    /// <returns>The predicate expression body (not wrapped in a lambda).</returns>
    Expression BuildExpressionCoreForTemplate<T>(
        PaginationColumn<T>[] columns,
        PaginationDirection direction,
        Expression[] referenceValueExpressions,
        ParameterExpression param);
}

/// <summary>
/// Base class for filter predicate strategies. Provides shared utilities for building
/// comparison expressions and extracting reference values from pagination columns.
/// </summary>
internal abstract class FilterPredicateStrategy : IFilterPredicateStrategy, IFilterPredicateStrategyInternal
{
    /// <summary>
    /// The default strategy instance used for predicate building.
    /// </summary>
    public static readonly FilterPredicateStrategy Default = FilterPredicateStrategyMethod1.Instance;

    /// <summary>
    /// Maps types that require <c>CompareTo</c> for ordering (instead of native <c>&lt;</c>/<c>&gt;</c> operators)
    /// to their <see cref="MethodInfo"/>.
    /// </summary>
    private static readonly FrozenDictionary<Type, MethodInfo> s_typeToCompareToMethod = new Dictionary<Type, MethodInfo>
    {
        { typeof(string), GetCompareToMethod(typeof(string)) },
        { typeof(Guid), GetCompareToMethod(typeof(Guid)) },
        { typeof(bool), GetCompareToMethod(typeof(bool)) },
    }.ToFrozenDictionary();

    /// <summary>
    /// Cached constant expression for the integer <c>0</c>, used in <c>CompareTo</c> comparisons.
    /// </summary>
    internal static ConstantExpression ZeroConstant { get; } = Expression.Constant(0);

    private static readonly ConstantExpression s_constantExpression0 = ZeroConstant;

    /// <inheritdoc />
    public Expression<Func<T, bool>> BuildFilterPredicateExpression<T>(
        PaginationColumn<T>[] columns,
        PaginationDirection direction,
        object reference)
    {
        var referenceValues = GetValues(columns, reference);
        var referenceValueBodies = new Expression[referenceValues.Length];
        for (var i = 0; i < referenceValues.Length; i++)
        {
            var holder = new ValueHolder { Value = referenceValues[i] };
            referenceValueBodies[i] = Expression.Field(
                Expression.Constant(holder), ValueHolder.ValueField);
        }

        var param = Expression.Parameter(typeof(T), "entity");
        var finalExpression = BuildExpressionCore(columns, direction, referenceValueBodies, param);
        return FastLambda<T>.Create(finalExpression, param);
    }

    /// <inheritdoc />
    Expression IFilterPredicateStrategyInternal.BuildExpressionCoreForTemplate<T>(
        PaginationColumn<T>[] columns,
        PaginationDirection direction,
        Expression[] referenceValueExpressions,
        ParameterExpression param) => BuildExpressionCore(columns, direction, referenceValueExpressions, param);

    internal Expression BuildExpressionCoreInternal<T>(
        PaginationColumn<T>[] columns,
        PaginationDirection direction,
        Expression[] referenceValueExpressions,
        ParameterExpression param)
        => BuildExpressionCore(columns, direction, referenceValueExpressions, param);

    /// <summary>
    /// Builds the predicate expression body. Implemented by each concrete strategy.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="columns">The pagination columns.</param>
    /// <param name="direction">The pagination direction.</param>
    /// <param name="referenceValueExpressions">Expressions representing reference values (either <see cref="ValueHolder"/> field accesses or placeholders).</param>
    /// <param name="param">The entity parameter expression for property access.</param>
    /// <returns>The predicate expression body (not wrapped in a lambda).</returns>
    protected abstract Expression BuildExpressionCore<T>(
        PaginationColumn<T>[] columns,
        PaginationDirection direction,
        Expression[] referenceValueExpressions,
        ParameterExpression param);

    /// <summary>
    /// Creates a <see cref="CachedPredicateTemplate{T}"/> for the specified columns using this strategy.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="columns">The pagination columns.</param>
    /// <returns>A new template cache instance.</returns>
    public CachedPredicateTemplate<T> CreateTemplate<T>(PaginationColumn<T>[] columns) => new(columns, this);

    /// <summary>
    /// Extracts the column values from a reference object using each column's <see cref="PaginationColumn{T}.ObtainValue{TReference}"/> method.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="columns">The pagination columns.</param>
    /// <param name="reference">The reference object to extract values from.</param>
    /// <returns>An array of boxed column values, one per column.</returns>
    internal static object[] GetValues<T>(
        PaginationColumn<T>[] columns,
        object reference)
    {
        var referenceValues = new object[columns.Length];
        for (var i = 0; i < columns.Length; i++)
        {
            referenceValues[i] = columns[i].ObtainValue(reference);
        }
        return referenceValues;
    }


    /// <summary>
    /// Builds a comparison expression for a pagination column, using <c>CompareTo</c> for types
    /// that lack native comparison operators (strings, GUIDs, booleans).
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="column">The pagination column being compared.</param>
    /// <param name="memberAccess">The expression accessing the entity's property.</param>
    /// <param name="referenceValue">The expression representing the reference value.</param>
    /// <param name="compare">The comparison factory (<c>GreaterThan</c>, <c>LessThan</c>, etc.).</param>
    /// <returns>A binary comparison expression.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static BinaryExpression MakeComparisonExpression<T>(
        PaginationColumn<T> column,
        Expression memberAccess, Expression referenceValue,
        Func<Expression, Expression, BinaryExpression> compare)
    {
        if (s_typeToCompareToMethod.TryGetValue(column.Type, out var compareToMethod))
        {
            var methodCallExpression = Expression.Call(
                memberAccess,
                compareToMethod,
                EnsureMatchingType(memberAccess, referenceValue));
            return compare(methodCallExpression, s_constantExpression0);
        }
        else
        {
            return compare(
                EnsureAdditionalConversions(memberAccess),
                EnsureAdditionalConversions(EnsureMatchingType(memberAccess, referenceValue)));
        }
    }

    /// <summary>
    /// Converts enum expressions to their underlying numeric type for comparison compatibility.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Expression EnsureAdditionalConversions(Expression expression)
    {
        if (expression.Type.IsEnum)
        {
            var underlyingType = Enum.GetUnderlyingType(expression.Type);
            return FastExpressions.Convert(expression, underlyingType);
        }

        return expression;
    }

    /// <summary>
    /// Inserts a type conversion if the target expression's type does not match the member expression's type.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static Expression EnsureMatchingType(
        Expression memberExpression,
        Expression targetExpression)
    {
        if (memberExpression.Type != targetExpression.Type)
        {
            return FastExpressions.Convert(targetExpression, memberExpression.Type);
        }

        return targetExpression;
    }

    /// <summary>
    /// Determines the comparison operator to apply based on direction and column sort order.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="direction">The pagination direction.</param>
    /// <param name="column">The pagination column.</param>
    /// <param name="orEqual">Whether to use an inclusive comparison (<c>&gt;=</c> or <c>&lt;=</c>).</param>
    /// <returns>A delegate that creates the appropriate <see cref="BinaryExpression"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static Func<Expression, Expression, BinaryExpression> GetComparisonExpressionToApply<T>(
        PaginationDirection direction, PaginationColumn<T> column, bool orEqual)
    {
        var greaterThan = (direction, column.IsDescending) switch
        {
            (PaginationDirection.Forward, false) => true,
            (PaginationDirection.Forward, true) => false,
            (PaginationDirection.Backward, false) => false,
            (PaginationDirection.Backward, true) => true,
            _ => throw new NotImplementedException(),
        };

        return orEqual ?
            (greaterThan ? Expression.GreaterThanOrEqual : Expression.LessThanOrEqual) :
            (greaterThan ? Expression.GreaterThan : Expression.LessThan);
    }

    /// <summary>
    /// Resolves the <c>CompareTo</c> method for the specified type via reflection.
    /// </summary>
    /// <param name="type">The type to resolve <c>CompareTo</c> for.</param>
    /// <returns>The <see cref="MethodInfo"/> for <c>CompareTo</c>.</returns>
    /// <exception cref="InvalidOperationException">If the type does not have a <c>CompareTo</c> method.</exception>
    protected static MethodInfo GetCompareToMethod(Type type)
    {
        var methodInfo = type.GetMethod(nameof(string.CompareTo), [type]) ?? throw new InvalidOperationException($"Didn't find a CompareTo method on type {type.Name}.");
        return methodInfo;
    }
}

/// <summary>
/// Strategy that builds the predicate as a disjunction of conjunctions:
/// <c>(col1 &gt; ref1) OR (col1 = ref1 AND col2 &gt; ref2) OR ...</c>.
/// Optionally prepends a first-column access predicate for database index utilization.
/// </summary>
internal sealed class FilterPredicateStrategyMethod1 : FilterPredicateStrategy
{
    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static readonly FilterPredicateStrategyMethod1 Instance = new();

    /// <summary>
    /// When <see langword="true"/>, prepends a <c>col1 &gt;= ref1</c> clause to enable
    /// the database to use an index seek on the first column.
    /// </summary>
    internal const bool EnableFirstColPredicateOpt = true;

    private FilterPredicateStrategyMethod1()
    {
    }

    /// <inheritdoc />
    protected override Expression BuildExpressionCore<T>(
        PaginationColumn<T>[] columns,
        PaginationDirection direction,
        Expression[] referenceValueExpressions,
        ParameterExpression param)
    {
        var memberAccessExpressions = new Expression[columns.Length];
        for (var i = 0; i < columns.Length; i++)
        {
            memberAccessExpressions[i] = columns[i].MakeAccessExpression(param);
        }

        Expression finalExpression;

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

                BinaryExpression innerExpression;
                if (!isInnerLastOperation)
                {
                    innerExpression = Expression.Equal(
                        memberAccess,
                        EnsureMatchingType(memberAccess, referenceValueExpression));
                }
                else
                {
                    var compare = GetComparisonExpressionToApply(direction, column, orEqual: false);
                    innerExpression = MakeComparisonExpression(
                        column,
                        memberAccess, referenceValueExpression,
                        compare);
                }

                andExpression = andExpression is null ? innerExpression : Expression.And(andExpression, innerExpression);
            }

            orExpression = orExpression is null ? andExpression : Expression.Or(orExpression, andExpression);

            innerLimit++;
        }

        finalExpression = orExpression;

        if (EnableFirstColPredicateOpt && columns.Length > 1)
        {
            var firstColumn = columns[0];
            var compare = GetComparisonExpressionToApply(direction, firstColumn, orEqual: true);
            var accessPredicateClause = MakeComparisonExpression(
                firstColumn,
                memberAccessExpressions[0], referenceValueExpressions[0],
                compare);
            finalExpression = Expression.And(accessPredicateClause, finalExpression);
        }

        return finalExpression;
    }
}

