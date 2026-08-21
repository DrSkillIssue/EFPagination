using System.Linq.Expressions;
using System.Runtime.InteropServices;
using EFPagination.Internal;
using Microsoft.EntityFrameworkCore;

namespace EFPagination;

/// <summary>
/// Extension methods for keyset (seek/cursor) pagination over <see cref="IQueryable{T}"/> sources.
/// </summary>
public static class PaginationExtensions
{
    private static readonly Task<bool> s_falseTask = Task.FromResult(false);

    /// <summary>
    /// Paginates using keyset pagination.
    /// </summary>
    /// <typeparam name="T">The type of the entity.</typeparam>
    /// <param name="source">An <see cref="IQueryable{T}"/> to paginate.</param>
    /// <param name="queryDefinition">The prebuilt pagination query definition.</param>
    /// <param name="direction">The direction to take. Default is Forward.</param>
    /// <param name="reference">The reference object. Needs to have properties with exact names matching the configured properties. Doesn't necessarily need to be the same type as T.</param>
    /// <returns>An object containing the modified queryable. Can be used with other helper methods related to pagination.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="queryDefinition"/> is null.</exception>
    /// <exception cref="InvalidOperationException">If no columns were registered with the definition.</exception>
    /// <exception cref="IncompatibleReferenceException"><paramref name="reference"/> is missing a property required by the pagination definition.</exception>
    public static PaginationContext<T> Paginate<T>(
        this IQueryable<T> source,
        PaginationQueryDefinition<T> queryDefinition,
        PaginationDirection direction = PaginationDirection.Forward,
        object? reference = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(queryDefinition);

        var bindings = reference is null ? null : BuildBindingsFromReference(queryDefinition.Columns, reference);
        return source.PaginateCore(queryDefinition.Columns, direction, bindings, queryDefinition.PredicateTemplate);
    }

    /// <summary>
    /// Paginates using keyset pagination with ordered values bound to the pagination definition.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="source">An <see cref="IQueryable{T}"/> to paginate.</param>
    /// <param name="queryDefinition">The prebuilt pagination query definition.</param>
    /// <param name="direction">The direction to take.</param>
    /// <param name="referenceValues">The definition-bound ordered values to use as the page boundary.</param>
    /// <returns>A <see cref="PaginationContext{T}"/> containing the ordered, optionally filtered query.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="queryDefinition"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No columns were registered with the definition.</exception>
    public static PaginationContext<T> Paginate<T>(
        this IQueryable<T> source,
        PaginationQueryDefinition<T> queryDefinition,
        PaginationDirection direction,
        PaginationValues<T> referenceValues)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(queryDefinition);

        return source.PaginateCore(queryDefinition.Columns, direction, referenceValues.Bindings, queryDefinition.PredicateTemplate);
    }

    /// <summary>
    /// Paginates using keyset pagination with a strongly-typed reference object.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <typeparam name="TReference">The type of the reference object.</typeparam>
    /// <param name="source">An <see cref="IQueryable{T}"/> to paginate.</param>
    /// <param name="queryDefinition">The prebuilt pagination query definition.</param>
    /// <param name="direction">The direction to take.</param>
    /// <param name="reference">The reference object. Must expose properties with exact names matching the configured columns.</param>
    /// <returns>A <see cref="PaginationContext{T}"/> containing the ordered, filtered query.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="queryDefinition"/>, or <paramref name="reference"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No columns were registered with the definition.</exception>
    /// <exception cref="IncompatibleReferenceException"><paramref name="reference"/> is missing a property required by the pagination definition.</exception>
    public static PaginationContext<T> Paginate<T, TReference>(
        this IQueryable<T> source,
        PaginationQueryDefinition<T> queryDefinition,
        PaginationDirection direction,
        TReference reference)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(queryDefinition);
        ArgumentNullException.ThrowIfNull(reference);

        var bindings = BuildBindingsFromReference(queryDefinition.Columns, reference!);
        return source.PaginateCore(queryDefinition.Columns, direction, bindings, queryDefinition.PredicateTemplate);
    }

    /// <summary>
    /// Paginates using keyset pagination with direct column name/value pairs.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="source">An <see cref="IQueryable{T}"/> to paginate.</param>
    /// <param name="queryDefinition">The prebuilt pagination query definition.</param>
    /// <param name="direction">The direction to take.</param>
    /// <param name="referenceValues">The column values to use as the pagination reference.</param>
    /// <returns>A <see cref="PaginationContext{T}"/> containing the ordered, optionally filtered query.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="queryDefinition"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="referenceValues"/> is missing a required column.</exception>
    /// <exception cref="InvalidOperationException">
    /// No columns were registered with the definition, or the direct-value path targets a definition column that cannot be addressed by name (computed/composite columns).
    /// </exception>
    public static PaginationContext<T> Paginate<T>(
        this IQueryable<T> source,
        PaginationQueryDefinition<T> queryDefinition,
        PaginationDirection direction,
        ReadOnlySpan<ColumnValue> referenceValues)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(queryDefinition);

        var bindings = referenceValues.IsEmpty
            ? null
            : BuildBindingsFromColumnValues(queryDefinition.Columns, referenceValues);
        return source.PaginateCore(queryDefinition.Columns, direction, bindings, queryDefinition.PredicateTemplate);
    }

    /// <summary>
    /// Array overload of
    /// <see cref="Paginate{T}(IQueryable{T}, PaginationQueryDefinition{T}, PaginationDirection, ReadOnlySpan{ColumnValue})"/>.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="source">An <see cref="IQueryable{T}"/> to paginate.</param>
    /// <param name="queryDefinition">The prebuilt pagination query definition.</param>
    /// <param name="direction">The direction to take.</param>
    /// <param name="referenceValues">The column values to use as the pagination reference.</param>
    /// <returns>A <see cref="PaginationContext{T}"/> containing the ordered, optionally filtered query.</returns>
    public static PaginationContext<T> Paginate<T>(
        this IQueryable<T> source,
        PaginationQueryDefinition<T> queryDefinition,
        PaginationDirection direction,
        ColumnValue[] referenceValues) => Paginate(source, queryDefinition, direction, referenceValues.AsSpan());

    private static PaginationContext<T> PaginateCore<T>(
        this IQueryable<T> source,
        PaginationColumn<T>[] columns,
        PaginationDirection direction,
        ColumnBinding[]? bindings,
        CachedPredicateTemplate<T>? predicateTemplate)
    {
        if (columns.Length == 0)
            throw new InvalidOperationException("There should be at least one configured column in the pagination definition.");

        using var activity = PaginationDiagnostics.StartPaginate(columns, direction, predicateTemplate is not null);

        var orderedQuery = columns[0].ApplyOrderBy(source, direction);
        for (var i = 1; i < columns.Length; i++)
            orderedQuery = columns[i].ApplyThenOrderBy(orderedQuery, direction);

        if (bindings is null || bindings.Length == 0)
            return new PaginationContext<T>(orderedQuery, orderedQuery, columns, direction, predicateTemplate);

        var lambda = predicateTemplate is not null
            ? predicateTemplate.Build(direction, bindings)
            : BuildFilterPredicateFromBindings(columns, direction, bindings);

        var filteredQuery = QueryableMethods.ApplyWhere(orderedQuery, lambda);
        return new PaginationContext<T>(filteredQuery, orderedQuery, columns, direction, predicateTemplate);
    }

    /// <summary>
    /// Determines whether more data exists before <paramref name="data"/> by issuing a single
    /// <c>EXISTS</c>-style query against the context's ordered query.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <typeparam name="T2">The element type of <paramref name="data"/>.</typeparam>
    /// <param name="context">The <see cref="PaginationContext{T}"/> returned by a <see cref="Paginate{T}(IQueryable{T}, PaginationQueryDefinition{T}, PaginationDirection, object?)"/> call.</param>
    /// <param name="data">The materialized page in correct order.</param>
    /// <returns>A task that resolves to <see langword="true"/> when more data exists before the supplied page.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="data"/> is <see langword="null"/>.</exception>
    /// <exception cref="IncompatibleReferenceException">The first element of <paramref name="data"/> is missing a property required by the pagination definition.</exception>
    public static Task<bool> HasPreviousAsync<T, T2>(
        this PaginationContext<T> context,
        IReadOnlyList<T2> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Count == 0) return s_falseTask;
        return HasAsync(context, PaginationDirection.Backward, data[0]!);
    }

    /// <summary>
    /// Determines whether more data exists after <paramref name="data"/> by issuing a single
    /// <c>EXISTS</c>-style query against the context's ordered query.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <typeparam name="T2">The element type of <paramref name="data"/>.</typeparam>
    /// <param name="context">The <see cref="PaginationContext{T}"/> returned by a <see cref="Paginate{T}(IQueryable{T}, PaginationQueryDefinition{T}, PaginationDirection, object?)"/> call.</param>
    /// <param name="data">The materialized page in correct order.</param>
    /// <returns>A task that resolves to <see langword="true"/> when more data exists after the supplied page.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="data"/> is <see langword="null"/>.</exception>
    /// <exception cref="IncompatibleReferenceException">The last element of <paramref name="data"/> is missing a property required by the pagination definition.</exception>
    public static Task<bool> HasNextAsync<T, T2>(
        this PaginationContext<T> context,
        IReadOnlyList<T2> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Count == 0) return s_falseTask;
        return HasAsync(context, PaginationDirection.Forward, data[^1]!);
    }

    private static Task<bool> HasAsync<T>(
        PaginationContext<T> context,
        PaginationDirection direction,
        object reference)
    {
        var bindings = BuildBindingsFromReference(context.Columns, reference);
        var lambda = context.PredicateTemplate is not null
            ? context.PredicateTemplate.Build(direction, bindings)
            : BuildFilterPredicateFromBindings(context.Columns, direction, bindings);
        return context.OrderedQuery.AnyAsync(lambda);
    }

    /// <summary>
    /// Ensures the data list is in correct presentation order. Reverses <paramref name="data"/>
    /// in-place when the context direction is <see cref="PaginationDirection.Backward"/>;
    /// otherwise no-op.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <typeparam name="T2">The element type of <paramref name="data"/>.</typeparam>
    /// <param name="context">The <see cref="PaginationContext{T}"/> whose direction informs the reversal.</param>
    /// <param name="data">The materialized page to reverse in-place when needed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="data"/> is <see langword="null"/>.</exception>
    public static void EnsureCorrectOrder<T, T2>(
        this PaginationContext<T> context,
        IList<T2> data)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (context.Direction != PaginationDirection.Backward)
            return;

        if (data is List<T2> list)
        {
            CollectionsMarshal.AsSpan(list).Reverse();
            return;
        }

        for (int i = 0, j = data.Count - 1; i < j; i++, j--)
            (data[i], data[j]) = (data[j], data[i]);
    }

    /// <summary>
    /// Returns a read-only view of items in correct presentation order. When the context
    /// direction is <see cref="PaginationDirection.Forward"/>, returns <paramref name="data"/>
    /// directly with zero allocation; for <see cref="PaginationDirection.Backward"/>, returns a
    /// reverse-indexed wrapper over the original list without copying.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <typeparam name="T2">The element type of <paramref name="data"/>.</typeparam>
    /// <param name="context">The <see cref="PaginationContext{T}"/> whose direction informs the view.</param>
    /// <param name="data">The read-only data list.</param>
    /// <returns>An <see cref="IReadOnlyList{T}"/> presenting items in correct presentation order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="data"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<T2> ToCorrectOrder<T, T2>(
        this PaginationContext<T> context,
        IReadOnlyList<T2> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return context.Direction == PaginationDirection.Backward
            ? new ReversedReadOnlyList<T2>(data)
            : data;
    }

    /// <summary>
    /// Materializes a paginated query, computing <see cref="KeysetPage{T}.HasPrevious"/> and
    /// <see cref="KeysetPage{T}.HasNext"/> without extra SQL round trips by leveraging the
    /// <c>pageSize + 1</c> overflow pattern and direction-aware inference.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="context">The pagination context returned by a <c>Paginate</c> call.</param>
    /// <param name="pageSize">The maximum number of items to return.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A materialized <see cref="KeysetPage{T}"/> in correct order with navigation flags populated.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageSize"/> is zero or negative.</exception>
    public static async Task<KeysetPage<T>> MaterializeAsync<T>(
        this PaginationContext<T> context,
        int pageSize,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);

        var (items, hasMore) = await PageMaterializer.MaterializeAsync(
            context.Query, pageSize, context.Direction, ct).ConfigureAwait(false);

        var isFiltered = !ReferenceEquals(context.Query, context.OrderedQuery);
        var hasPrevious = context.Direction == PaginationDirection.Forward ? isFiltered : hasMore;
        var hasNext = context.Direction == PaginationDirection.Forward ? hasMore : isFiltered;

        return new KeysetPage<T>(items, hasPrevious, hasNext);
    }

    internal static ColumnBinding[] BuildBindingsFromReference<T>(
        PaginationColumn<T>[] columns,
        object reference)
    {
        var bindings = new ColumnBinding[columns.Length];
        for (var i = 0; i < columns.Length; i++)
        {
            var binding = columns[i].CreateBinding();
            columns[i].WriteBindingFromReference(reference, binding);
            bindings[i] = binding;
        }
        return bindings;
    }

    internal static ColumnBinding[] BuildBindingsFromColumnValues<T>(
        PaginationColumn<T>[] columns,
        ReadOnlySpan<ColumnValue> referenceValues)
    {
        var bindings = new ColumnBinding[columns.Length];

        if (referenceValues.Length == columns.Length)
        {
            var positional = true;
            for (var i = 0; i < columns.Length; i++)
            {
                if (!string.Equals(referenceValues[i].Name, columns[i].GetRequiredPropertyNameForColumnValues(), StringComparison.OrdinalIgnoreCase))
                {
                    positional = false;
                    break;
                }
            }

            if (positional)
            {
                for (var i = 0; i < columns.Length; i++)
                {
                    var binding = columns[i].CreateBinding();
                    columns[i].WriteBindingFromBoxed(referenceValues[i].Value, binding);
                    bindings[i] = binding;
                }
                return bindings;
            }
        }

        for (var i = 0; i < columns.Length; i++)
        {
            var columnName = columns[i].GetRequiredPropertyNameForColumnValues();
            var found = false;
            for (var j = 0; j < referenceValues.Length; j++)
            {
                if (string.Equals(referenceValues[j].Name, columnName, StringComparison.OrdinalIgnoreCase))
                {
                    var binding = columns[i].CreateBinding();
                    columns[i].WriteBindingFromBoxed(referenceValues[j].Value, binding);
                    bindings[i] = binding;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                throw new ArgumentException(
                    $"No value provided for pagination column '{columnName}'.", nameof(referenceValues));
            }
        }

        return bindings;
    }

    private static Expression<Func<T, bool>> BuildFilterPredicateFromBindings<T>(
        PaginationColumn<T>[] columns,
        PaginationDirection direction,
        ColumnBinding[] bindings)
    {
        var referenceValueBodies = new Expression[bindings.Length];
        for (var i = 0; i < bindings.Length; i++)
            referenceValueBodies[i] = bindings[i].CreateValueAccessExpression();

        var param = Expression.Parameter(typeof(T), "entity");
        var body = FilterPredicateStrategy.BuildExpressionCore(
            columns, direction, referenceValueBodies, param);
        return FastLambda<T>.Create(body, param);
    }

}
