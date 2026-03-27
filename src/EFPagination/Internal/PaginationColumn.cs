using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace EFPagination.Internal;

/// <summary>
/// Represents a single column in a pagination definition, with type-erased column type.
/// Provides abstract operations for ordering, property access, and value extraction.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
internal abstract class PaginationColumn<T>(
    bool isDescending,
    LambdaExpression lambdaExpression)
{
    /// <summary>
    /// Gets whether this column is sorted in descending order.
    /// </summary>
    public bool IsDescending { get; } = isDescending;

    /// <summary>
    /// Gets the lambda expression that accesses this column's property on the entity.
    /// </summary>
    public LambdaExpression LambdaExpression { get; } = lambdaExpression;

    /// <summary>
    /// Gets the CLR type of this column's value. Lazily resolved from the lambda body.
    /// </summary>
    public Type Type => field ??= LambdaExpression.Body.Type;

    /// <summary>
    /// Gets the property name for this column when it is directly addressable by name.
    /// </summary>
    public string? PropertyName { get; } = ResolvePropertyName(lambdaExpression);

    /// <summary>
    /// Creates an expression that accesses this column's property using the given entity parameter.
    /// </summary>
    /// <param name="parameter">The entity parameter expression to bind to.</param>
    /// <returns>An expression representing the property access.</returns>
    public abstract Expression MakeAccessExpression(ParameterExpression parameter);

    /// <summary>
    /// Applies an initial <c>OrderBy</c> or <c>OrderByDescending</c> to the query for this column.
    /// </summary>
    /// <param name="query">The source queryable.</param>
    /// <param name="direction">The pagination direction, which may invert the sort order.</param>
    /// <returns>An ordered queryable.</returns>
    public abstract IOrderedQueryable<T> ApplyOrderBy(IQueryable<T> query, PaginationDirection direction);

    /// <summary>
    /// Applies a subsequent <c>ThenBy</c> or <c>ThenByDescending</c> to the query for this column.
    /// </summary>
    /// <param name="query">The already-ordered queryable.</param>
    /// <param name="direction">The pagination direction, which may invert the sort order.</param>
    /// <returns>An ordered queryable with this column appended.</returns>
    public abstract IOrderedQueryable<T> ApplyThenOrderBy(IOrderedQueryable<T> query, PaginationDirection direction);

    /// <summary>
    /// Extracts this column's value from a reference object, supporting loose typing
    /// (the reference does not need to be of type <typeparamref name="T"/>).
    /// </summary>
    /// <typeparam name="TReference">The type of the reference object.</typeparam>
    /// <param name="reference">The reference object to extract the value from.</param>
    /// <returns>The boxed column value.</returns>
    public abstract object ObtainValue<TReference>(TReference reference);

    public string GetRequiredPropertyNameForColumnValues()
    {
        return PropertyName ?? throw new InvalidOperationException(
            $"Direct column-value pagination only supports member-access columns. Expression '{LambdaExpression.Body}' is not addressable by column name.");
    }

    private static string? ResolvePropertyName(LambdaExpression lambda)
    {
        var body = lambda.Body;
        if (body is UnaryExpression { NodeType: ExpressionType.Convert } unary)
            body = unary.Operand;

        if (body is not MemberExpression memberExpression)
            return null;

        var segmentCount = 0;
        var totalLength = 0;
        Expression? current = memberExpression;

        while (current is MemberExpression currentMember)
        {
            if (currentMember.Member is not PropertyInfo property)
            {
                throw new InvalidOperationException("Pagination column member-access chain must contain only properties.");
            }

            totalLength += property.Name.Length;
            segmentCount++;
            current = currentMember.Expression;
        }

        if (segmentCount == 1)
            return ((PropertyInfo)memberExpression.Member).Name;

        return string.Create(totalLength + segmentCount - 1, memberExpression, static (span, state) =>
        {
            WritePropertyPath(span, state, 0);
        });
    }

    private static int WritePropertyPath(Span<char> destination, MemberExpression memberExpression, int position)
    {
        if (memberExpression.Expression is MemberExpression parent)
        {
            position = WritePropertyPath(destination, parent, position);
            destination[position++] = '.';
        }

        var property = memberExpression.Member as PropertyInfo
            ?? throw new InvalidOperationException("Pagination column member-access chain must contain only properties.");

        property.Name.AsSpan().CopyTo(destination[position..]);
        return position + property.Name.Length;
    }
}

/// <summary>
/// Concrete implementation of <see cref="PaginationColumn{T}"/> with a strongly-typed column accessor.
/// Caches compiled lambda delegates for value extraction and OrderBy lambda expressions.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
/// <typeparam name="TColumn">The CLR type of this column's value.</typeparam>
internal sealed class PaginationColumn<T, TColumn>(
    bool isDescending,
    Expression<Func<T, TColumn>> expression) : PaginationColumn<T>(isDescending, expression)
{
    private readonly ConcurrentDictionary<Type, Func<object, TColumn>> _referenceTypeToCompiledAccessMap = new();
    private volatile Type? _lastAccessType;
    private volatile Func<object, TColumn>? _lastAccessFunc;
    private Expression<Func<T, TColumn>>? _cachedOrderByLambda;

    /// <summary>
    /// Gets the strongly-typed lambda expression for this column.
    /// </summary>
    public new Expression<Func<T, TColumn>> LambdaExpression => (Expression<Func<T, TColumn>>)base.LambdaExpression;

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override Expression MakeAccessExpression(ParameterExpression parameter) => AdaptingExpressionVisitor.AdaptParameter(LambdaExpression, parameter).Body;

    /// <summary>
    /// Returns the cached OrderBy lambda, building it on first access by adapting the column's
    /// lambda expression to a fresh parameter.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Expression<Func<T, TColumn>> GetOrderByLambda()
    {
        return _cachedOrderByLambda ??= AdaptingExpressionVisitor.AdaptParameter(
            LambdaExpression,
            Expression.Parameter(typeof(T), "x"));
    }

    /// <inheritdoc />
    public override IOrderedQueryable<T> ApplyOrderBy(IQueryable<T> query, PaginationDirection direction)
    {
        var lambda = GetOrderByLambda();
        var descending = direction == PaginationDirection.Backward ? !IsDescending : IsDescending;
        return descending ? Queryable.OrderByDescending(query, lambda) : Queryable.OrderBy(query, lambda);
    }

    /// <inheritdoc />
    public override IOrderedQueryable<T> ApplyThenOrderBy(IOrderedQueryable<T> query, PaginationDirection direction)
    {
        var lambda = GetOrderByLambda();
        var descending = direction == PaginationDirection.Backward ? !IsDescending : IsDescending;
        return descending ? Queryable.ThenByDescending(query, lambda) : Queryable.ThenBy(query, lambda);
    }

    /// <inheritdoc />
    public override object ObtainValue<TReference>(TReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        var referenceType = reference.GetType();

        var lastType = _lastAccessType;
        var lastFunc = _lastAccessFunc;
        if (lastType == referenceType && lastFunc is not null)
        {
            return lastFunc(reference)!;
        }

        var compiledAccess = _referenceTypeToCompiledAccessMap.GetOrAdd(
            referenceType,
            static (type, lambdaExpr) =>
            {
                var adapted = AdaptingExpressionVisitor.AdaptType(lambdaExpr, type);
                return adapted.Compile();
            },
            LambdaExpression);

        _lastAccessType = referenceType;
        _lastAccessFunc = compiledAccess;
        return compiledAccess(reference)!;
    }
}
