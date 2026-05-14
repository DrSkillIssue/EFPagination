using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using EFPagination.Cursor;

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
    /// Whether this column's value can legitimately be <see langword="null"/>.
    /// </summary>
    public abstract bool IsNullable { get; }

    /// <summary>
    /// Writes this column's value to the cursor by extracting it from <paramref name="reference"/>
    /// via the cached typed accessor.
    /// </summary>
    /// <param name="reference">The entity or reference object supplying the value.</param>
    /// <param name="writer">The cursor writer to append the encoded bytes to.</param>
    /// <returns>
    /// <see langword="false"/> when the extracted value is <see langword="null"/> (caller should
    /// record the null bit in the bitmap); otherwise <see langword="true"/>.
    /// </returns>
    public abstract bool TryWriteCursorValueFromReference(object reference, ref CursorWriter writer);

    /// <summary>
    /// Writes a binding's typed value to the cursor without boxing.
    /// </summary>
    /// <param name="binding">The typed binding holding the value.</param>
    /// <param name="writer">The cursor writer to append the encoded bytes to.</param>
    /// <returns>
    /// <see langword="false"/> when the binding holds <see langword="null"/> (caller should record
    /// the null bit in the bitmap); otherwise <see langword="true"/>.
    /// </returns>
    public abstract bool TryWriteCursorValueFromBinding(ColumnBinding binding, ref CursorWriter writer);

    /// <summary>
    /// Allocates a fresh typed <see cref="ColumnBinding"/> for this column.
    /// </summary>
    /// <returns>A new <see cref="ColumnBinding"/> instance typed to this column's CLR type.</returns>
    public abstract ColumnBinding CreateBinding();

    /// <summary>
    /// Reads this column's typed value from the cursor and stores it in <paramref name="binding"/>
    /// without intermediate boxing.
    /// </summary>
    /// <param name="reader">The cursor reader positioned at this column's value.</param>
    /// <param name="binding">The destination binding to receive the decoded value.</param>
    public abstract void DecodeCursorValueInto(ref CursorReader reader, ColumnBinding binding);

    /// <summary>
    /// Writes <paramref name="binding"/>'s value from a boxed value. Used when the source value
    /// arrived already boxed (such as via <see cref="ColumnValue.Value"/>).
    /// </summary>
    /// <param name="boxed">The boxed source value, or <see langword="null"/>.</param>
    /// <param name="binding">The destination binding to receive the value.</param>
    /// <exception cref="InvalidOperationException"><paramref name="boxed"/> is <see langword="null"/> for a non-nullable column type.</exception>
    public abstract void WriteBindingFromBoxed(object? boxed, ColumnBinding binding);

    /// <summary>
    /// Extracts this column's value from <paramref name="reference"/> and stores it in
    /// <paramref name="binding"/> typed, without boxing.
    /// </summary>
    /// <param name="reference">The entity or reference object supplying the value.</param>
    /// <param name="binding">The destination binding to receive the value.</param>
    public abstract void WriteBindingFromReference(object reference, ColumnBinding binding);

    /// <summary>
    /// Returns <see cref="PropertyName"/> for column-name-addressable columns.
    /// </summary>
    /// <returns>The resolved property path.</returns>
    /// <exception cref="InvalidOperationException">This column is not addressable by name (computed or non-member expression).</exception>
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

    // Caller (ResolvePropertyName) already validated every chain member is a PropertyInfo.
    private static int WritePropertyPath(Span<char> destination, MemberExpression memberExpression, int position)
    {
        if (memberExpression.Expression is MemberExpression parent)
        {
            position = WritePropertyPath(destination, parent, position);
            destination[position++] = '.';
        }

        var name = ((PropertyInfo)memberExpression.Member).Name;
        name.AsSpan().CopyTo(destination[position..]);
        return position + name.Length;
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
    private static readonly MethodInfo s_orderBy = QueryableMethods.Get(nameof(Queryable.OrderBy), 2).MakeGenericMethod(typeof(T), typeof(TColumn));
    private static readonly MethodInfo s_orderByDesc = QueryableMethods.Get(nameof(Queryable.OrderByDescending), 2).MakeGenericMethod(typeof(T), typeof(TColumn));
    private static readonly MethodInfo s_thenBy = QueryableMethods.Get(nameof(Queryable.ThenBy), 2).MakeGenericMethod(typeof(T), typeof(TColumn));
    private static readonly MethodInfo s_thenByDesc = QueryableMethods.Get(nameof(Queryable.ThenByDescending), 2).MakeGenericMethod(typeof(T), typeof(TColumn));
    private static readonly bool s_isColumnNullable = !typeof(TColumn).IsValueType || Nullable.GetUnderlyingType(typeof(TColumn)) is not null;
    private static readonly EnumIo<TColumn>? s_enumIo = EnumIo<TColumn>.Instance;

    private readonly ConcurrentDictionary<Type, Func<object, TColumn>> _referenceTypeToCompiledAccessMap = new();
    private volatile Type? _lastAccessType;
    private volatile Func<object, TColumn>? _lastAccessFunc;
    private Expression<Func<T, TColumn>>? _cachedOrderByLambda;

    public new Expression<Func<T, TColumn>> LambdaExpression => (Expression<Func<T, TColumn>>)base.LambdaExpression;

    public override bool IsNullable => s_isColumnNullable;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override Expression MakeAccessExpression(ParameterExpression parameter) => AdaptingExpressionVisitor.AdaptParameter(LambdaExpression, parameter).Body;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Expression<Func<T, TColumn>> GetOrderByLambda()
    {
        return _cachedOrderByLambda ??= AdaptingExpressionVisitor.AdaptParameter(
            LambdaExpression,
            Expression.Parameter(typeof(T), "x"));
    }

    public override IOrderedQueryable<T> ApplyOrderBy(IQueryable<T> query, PaginationDirection direction)
    {
        var descending = direction == PaginationDirection.Backward ? !IsDescending : IsDescending;
        return ApplyDirect(query, descending ? s_orderByDesc : s_orderBy);
    }

    public override IOrderedQueryable<T> ApplyThenOrderBy(IOrderedQueryable<T> query, PaginationDirection direction)
    {
        var descending = direction == PaginationDirection.Backward ? !IsDescending : IsDescending;
        return ApplyDirect(query, descending ? s_thenByDesc : s_thenBy);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IOrderedQueryable<T> ApplyDirect(IQueryable<T> query, MethodInfo method)
    {
        var lambda = GetOrderByLambda();
        var call = Expression.Call(method, query.Expression, Expression.Quote(lambda));
        return (IOrderedQueryable<T>)query.Provider.CreateQuery<T>(call);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TColumn ObtainValueTyped(object reference)
    {
        var referenceType = reference.GetType();

        var lastType = _lastAccessType;
        var lastFunc = _lastAccessFunc;
        if (lastType == referenceType && lastFunc is not null)
        {
            return lastFunc(reference);
        }

        return ResolveAccessor(referenceType)(reference);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private Func<object, TColumn> ResolveAccessor(Type referenceType)
    {
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
        return compiledAccess;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool TryWriteCursorValueFromReference(object reference, ref CursorWriter writer)
    {
        var value = ObtainValueTyped(reference);
        if (default(TColumn) is null && value is null)
            return false;
        WriteValue(ref writer, value);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool TryWriteCursorValueFromBinding(ColumnBinding binding, ref CursorWriter writer)
    {
        var value = Unsafe.As<ColumnBinding<TColumn>>(binding).Value;
        if (default(TColumn) is null && value is null)
            return false;
        WriteValue(ref writer, value);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override ColumnBinding CreateBinding() => new ColumnBinding<TColumn>();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void DecodeCursorValueInto(ref CursorReader reader, ColumnBinding binding)
    {
        Unsafe.As<ColumnBinding<TColumn>>(binding).Value = ReadValue(ref reader);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteValue(ref CursorWriter writer, TColumn value)
    {
        if (s_enumIo is not null) s_enumIo.Write(ref writer, value);
        else TypedCursorIo.Write(ref writer, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TColumn ReadValue(ref CursorReader reader)
        => s_enumIo is not null ? s_enumIo.Read(ref reader) : TypedCursorIo.Read<TColumn>(ref reader);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void WriteBindingFromBoxed(object? boxed, ColumnBinding binding)
    {
        var typed = Unsafe.As<ColumnBinding<TColumn>>(binding);
        if (boxed is null)
        {
            if (default(TColumn) is not null)
                ThrowNullForNonNullable();
            typed.Value = default!;
        }
        else
        {
            typed.Value = (TColumn)boxed;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void WriteBindingFromReference(object reference, ColumnBinding binding)
    {
        Unsafe.As<ColumnBinding<TColumn>>(binding).Value = ObtainValueTyped(reference);
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowNullForNonNullable() =>
        throw new InvalidOperationException(
            $"Cannot bind a null value to non-nullable pagination column of type '{typeof(TColumn)}'.");
}
