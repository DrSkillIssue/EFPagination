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
    /// <exception cref="ArgumentNullException">
    /// <paramref name="source"/> is null.
    /// <paramref name="queryDefinition"/> is null.
    /// </exception>
    /// <exception cref="InvalidOperationException">If no columns were registered with the builder.</exception>
    /// <remarks>
    /// Note that calling this method will override any OrderBy calls you have done before.
    /// </remarks>
    public static PaginationContext<T> Paginate<T>(
        this IQueryable<T> source,
        PaginationQueryDefinition<T> queryDefinition,
        PaginationDirection direction = PaginationDirection.Forward,
        object? reference = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(queryDefinition);

        return source.Paginate(queryDefinition.Columns, direction, reference, queryDefinition.PredicateTemplate);
    }

    /// <summary>
    /// Paginates using keyset pagination with a strongly-typed reference object.
    /// </summary>
    /// <typeparam name="T">The type of the entity.</typeparam>
    /// <typeparam name="TReference">The type of the reference object.</typeparam>
    /// <param name="source">An <see cref="IQueryable{T}"/> to paginate.</param>
    /// <param name="queryDefinition">The prebuilt pagination query definition.</param>
    /// <param name="direction">The direction to take.</param>
    /// <param name="reference">The reference object. Needs to have properties with exact names matching the configured properties.</param>
    /// <returns>An object containing the modified queryable.</returns>
    public static PaginationContext<T> Paginate<T, TReference>(
        this IQueryable<T> source,
        PaginationQueryDefinition<T> queryDefinition,
        PaginationDirection direction,
        TReference reference)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(queryDefinition);
        ArgumentNullException.ThrowIfNull(reference);

        return source.Paginate(queryDefinition.Columns, direction, reference, queryDefinition.PredicateTemplate);
    }

    /// <summary>
    /// Paginates using keyset pagination with direct column values.
    /// </summary>
    /// <typeparam name="T">The type of the entity.</typeparam>
    /// <param name="source">An <see cref="IQueryable{T}"/> to paginate.</param>
    /// <param name="queryDefinition">The prebuilt pagination query definition.</param>
    /// <param name="direction">The direction to take.</param>
    /// <param name="referenceValues">The column values to use as the pagination reference.</param>
    /// <returns>An object containing the modified queryable.</returns>
    public static PaginationContext<T> Paginate<T>(
        this IQueryable<T> source,
        PaginationQueryDefinition<T> queryDefinition,
        PaginationDirection direction,
        ReadOnlySpan<ColumnValue> referenceValues)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(queryDefinition);

        return source.Paginate(queryDefinition.Columns, direction, referenceValues, queryDefinition.PredicateTemplate);
    }

    /// <summary>
    /// Paginates using keyset pagination with direct column values.
    /// </summary>
    /// <typeparam name="T">The type of the entity.</typeparam>
    /// <param name="source">An <see cref="IQueryable{T}"/> to paginate.</param>
    /// <param name="queryDefinition">The prebuilt pagination query definition.</param>
    /// <param name="direction">The direction to take.</param>
    /// <param name="referenceValues">The column values to use as the pagination reference.</param>
    /// <returns>An object containing the modified queryable.</returns>
    public static PaginationContext<T> Paginate<T>(
        this IQueryable<T> source,
        PaginationQueryDefinition<T> queryDefinition,
        PaginationDirection direction,
        ColumnValue[] referenceValues) => Paginate(source, queryDefinition, direction, referenceValues.AsSpan());

    /// <summary>
    /// Paginates using keyset pagination.
    /// </summary>
    /// <typeparam name="T">The type of the entity.</typeparam>
    /// <param name="source">An <see cref="IQueryable{T}"/> to paginate.</param>
    /// <param name="builderAction">An action that takes a builder and registers the columns upon which pagination will work.</param>
    /// <param name="direction">The direction to take. Default is Forward.</param>
    /// <param name="reference">The reference object. Needs to have properties with exact names matching the configured properties. Doesn't necessarily need to be the same type as T.</param>
    /// <returns>An object containing the modified queryable. Can be used with other helper methods related to pagination.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="source"/> is null.
    /// <paramref name="builderAction"/> is null.
    /// </exception>
    /// <exception cref="InvalidOperationException">If no columns were registered with the builder.</exception>
    /// <remarks>
    /// Note that calling this method will override any OrderBy calls you have done before.
    /// </remarks>
    public static PaginationContext<T> Paginate<T>(
        this IQueryable<T> source,
        Action<PaginationBuilder<T>> builderAction,
        PaginationDirection direction = PaginationDirection.Forward,
        object? reference = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(builderAction);

        var columns = PaginationQuery.BuildColumns(builderAction);
        return source.Paginate(columns, direction, reference);
    }

    /// <summary>
    /// Paginates using keyset pagination with an inline builder and strongly-typed reference object.
    /// </summary>
    /// <typeparam name="T">The type of the entity.</typeparam>
    /// <typeparam name="TReference">The type of the reference object.</typeparam>
    /// <param name="source">An <see cref="IQueryable{T}"/> to paginate.</param>
    /// <param name="builderAction">An action that configures the pagination columns.</param>
    /// <param name="direction">The direction to take.</param>
    /// <param name="reference">The reference object. Needs to have properties with exact names matching the configured properties.</param>
    /// <returns>An object containing the modified queryable.</returns>
    public static PaginationContext<T> Paginate<T, TReference>(
        this IQueryable<T> source,
        Action<PaginationBuilder<T>> builderAction,
        PaginationDirection direction,
        TReference reference)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(builderAction);
        ArgumentNullException.ThrowIfNull(reference);

        var columns = PaginationQuery.BuildColumns(builderAction);
        return source.Paginate(columns, direction, reference);
    }

    private static PaginationContext<T> Paginate<T>(
        this IQueryable<T> source,
        PaginationColumn<T>[] columns,
        PaginationDirection direction,
        object? reference,
        CachedPredicateTemplate<T>? predicateTemplate = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (columns.Length == 0)
        {
            throw new InvalidOperationException("There should be at least one configured column in the pagination definition.");
        }

        // Order

        var orderedQuery = columns[0].ApplyOrderBy(source, direction);
        for (var i = 1; i < columns.Length; i++)
        {
            orderedQuery = columns[i].ApplyThenOrderBy(orderedQuery, direction);
        }

        // Filter

        IQueryable<T> filteredQuery = orderedQuery;
        if (reference is not null)
        {
            var filterPredicate = predicateTemplate is not null
                ? BuildFilterPredicateExpressionCached(predicateTemplate, columns, direction, reference)
                : BuildFilterPredicateExpression(columns, direction, reference);
            filteredQuery = filteredQuery.Where(filterPredicate);
        }

        return new PaginationContext<T>(filteredQuery, orderedQuery, columns, direction, predicateTemplate);
    }

    private static PaginationContext<T> Paginate<T, TReference>(
        this IQueryable<T> source,
        PaginationColumn<T>[] columns,
        PaginationDirection direction,
        TReference reference,
        CachedPredicateTemplate<T>? predicateTemplate = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(reference);

        if (columns.Length == 0)
        {
            throw new InvalidOperationException("There should be at least one configured column in the pagination definition.");
        }

        var orderedQuery = columns[0].ApplyOrderBy(source, direction);
        for (var i = 1; i < columns.Length; i++)
        {
            orderedQuery = columns[i].ApplyThenOrderBy(orderedQuery, direction);
        }

        var filterPredicate = predicateTemplate is not null
            ? BuildFilterPredicateExpressionCached(predicateTemplate, columns, direction, reference)
            : BuildFilterPredicateExpression(columns, direction, reference);
        var filteredQuery = orderedQuery.Where(filterPredicate);

        return new PaginationContext<T>(filteredQuery, orderedQuery, columns, direction, predicateTemplate);
    }

    private static PaginationContext<T> Paginate<T>(
        this IQueryable<T> source,
        PaginationColumn<T>[] columns,
        PaginationDirection direction,
        ReadOnlySpan<ColumnValue> referenceValues,
        CachedPredicateTemplate<T>? predicateTemplate = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (columns.Length == 0)
        {
            throw new InvalidOperationException("There should be at least one configured column in the pagination definition.");
        }

        var orderedQuery = columns[0].ApplyOrderBy(source, direction);
        for (var i = 1; i < columns.Length; i++)
        {
            orderedQuery = columns[i].ApplyThenOrderBy(orderedQuery, direction);
        }

        IQueryable<T> filteredQuery = orderedQuery;
        if (!referenceValues.IsEmpty)
        {
            Expression<Func<T, bool>> filterPredicate;

            if (predicateTemplate is not null)
            {
                var template = direction == PaginationDirection.Forward
                    ? predicateTemplate.ForwardTemplate
                    : predicateTemplate.BackwardTemplate;
                filterPredicate = template.InstantiateFromColumnValues(columns, referenceValues);
            }
            else
            {
                var orderedValues = OrderValuesByColumns(columns, referenceValues);
                filterPredicate = BuildFilterPredicateExpressionFromValues(columns, direction, orderedValues);
            }

            filteredQuery = filteredQuery.Where(filterPredicate);
        }

        return new PaginationContext<T>(filteredQuery, orderedQuery, columns, direction, predicateTemplate);
    }

    /// <summary>
    /// Paginates using keyset pagination.
    /// </summary>
    /// <typeparam name="T">The type of the entity.</typeparam>
    /// <param name="source">An <see cref="IQueryable{T}"/> to paginate.</param>
    /// <param name="queryDefinition">The prebuilt pagination query definition.</param>
    /// <param name="direction">The direction to take. Default is Forward.</param>
    /// <param name="reference">The reference object. Needs to have properties with exact names matching the configured properties. Doesn't necessarily need to be the same type as T.</param>
    /// <returns>The modified the queryable.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="source"/> is null.
    /// <paramref name="queryDefinition"/> is null.
    /// </exception>
    /// <exception cref="InvalidOperationException">If no properties were registered with the builder.</exception>
    /// <remarks>
    /// Note that calling this method will override any OrderBy calls you have done before.
    /// </remarks>
    public static IQueryable<T> PaginateQuery<T>(
        this IQueryable<T> source,
        PaginationQueryDefinition<T> queryDefinition,
        PaginationDirection direction = PaginationDirection.Forward,
        object? reference = null) => Paginate(source, queryDefinition, direction, reference).Query;

    /// <summary>
    /// Paginates using keyset pagination with a strongly-typed reference object and returns the query directly.
    /// </summary>
    public static IQueryable<T> PaginateQuery<T, TReference>(
        this IQueryable<T> source,
        PaginationQueryDefinition<T> queryDefinition,
        PaginationDirection direction,
        TReference reference) => Paginate(source, queryDefinition, direction, reference).Query;

    /// <summary>
    /// Paginates using keyset pagination with direct column values and returns the query directly.
    /// </summary>
    /// <typeparam name="T">The type of the entity.</typeparam>
    /// <param name="source">An <see cref="IQueryable{T}"/> to paginate.</param>
    /// <param name="queryDefinition">The prebuilt pagination query definition.</param>
    /// <param name="direction">The direction to take.</param>
    /// <param name="referenceValues">The column values to use as the pagination reference.</param>
    /// <returns>The modified queryable.</returns>
    public static IQueryable<T> PaginateQuery<T>(
        this IQueryable<T> source,
        PaginationQueryDefinition<T> queryDefinition,
        PaginationDirection direction,
        ReadOnlySpan<ColumnValue> referenceValues) => Paginate(source, queryDefinition, direction, referenceValues).Query;

    /// <summary>
    /// Paginates using keyset pagination with direct column values from an array, then returns the query directly.
    /// </summary>
    public static IQueryable<T> PaginateQuery<T>(
        this IQueryable<T> source,
        PaginationQueryDefinition<T> queryDefinition,
        PaginationDirection direction,
        ColumnValue[] referenceValues) => Paginate(source, queryDefinition, direction, referenceValues).Query;

    /// <summary>
    /// Paginates using keyset pagination.
    /// </summary>
    /// <typeparam name="T">The type of the entity.</typeparam>
    /// <param name="source">An <see cref="IQueryable{T}"/> to paginate.</param>
    /// <param name="builderAction">An action that takes a builder and registers the columns upon which pagination will work.</param>
    /// <param name="direction">The direction to take. Default is Forward.</param>
    /// <param name="reference">The reference object. Needs to have properties with exact names matching the configured properties. Doesn't necessarily need to be the same type as T.</param>
    /// <returns>The modified the queryable.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="source"/> is null.
    /// <paramref name="builderAction"/> is null.
    /// </exception>
    /// <exception cref="InvalidOperationException">If no properties were registered with the builder.</exception>
    /// <remarks>
    /// Note that calling this method will override any OrderBy calls you have done before.
    /// </remarks>
    public static IQueryable<T> PaginateQuery<T>(
        this IQueryable<T> source,
        Action<PaginationBuilder<T>> builderAction,
        PaginationDirection direction = PaginationDirection.Forward,
        object? reference = null) => Paginate(source, builderAction, direction, reference).Query;

    /// <summary>
    /// Paginates using keyset pagination with an inline builder and strongly-typed reference object, then returns the query directly.
    /// </summary>
    public static IQueryable<T> PaginateQuery<T, TReference>(
        this IQueryable<T> source,
        Action<PaginationBuilder<T>> builderAction,
        PaginationDirection direction,
        TReference reference) => Paginate(source, builderAction, direction, reference).Query;

    /// <summary>
    /// Returns true when there is more data before the list.
    /// </summary>
    /// <typeparam name="T">The type of the entity.</typeparam>
    /// <typeparam name="T2">The type of the elements of the data.</typeparam>
    /// <param name="context">The <see cref="PaginationContext{T}"/> object.</param>
    /// <param name="data">The data list.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/> is null.
    /// <paramref name="data"/> is null.
    /// </exception>
    public static Task<bool> HasPreviousAsync<T, T2>(
        this PaginationContext<T> context,
        IReadOnlyList<T2> data)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(data);

        if (data.Count == 0)
        {
            return s_falseTask;
        }

        // Get first item and see if there's anything before it.
        var reference = data[0]!;
        return HasAsync(context, PaginationDirection.Backward, reference);
    }

    /// <summary>
    /// Returns true when there is more data after the list.
    /// </summary>
    /// <typeparam name="T">The type of the entity.</typeparam>
    /// <typeparam name="T2">The type of the elements of the data.</typeparam>
    /// <param name="context">The <see cref="PaginationContext{T}"/> object.</param>
    /// <param name="data">The data list.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/> is null.
    /// <paramref name="data"/> is null.
    /// </exception>
    public static Task<bool> HasNextAsync<T, T2>(
        this PaginationContext<T> context,
        IReadOnlyList<T2> data)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(data);

        if (data.Count == 0)
        {
            return s_falseTask;
        }

        // Get last item and see if there's anything after it.
        var reference = data[^1]!;
        return HasAsync(context, PaginationDirection.Forward, reference);
    }

    private static Task<bool> HasAsync<T, TReference>(
        this PaginationContext<T> context,
        PaginationDirection direction,
        TReference reference)
    {
        var lambda = context.PredicateTemplate is not null
            ? BuildFilterPredicateExpressionCached(context.PredicateTemplate, context.Columns, direction, reference)
            : BuildFilterPredicateExpression(context.Columns, direction, reference);
        return context.OrderedQuery.AnyAsync(lambda);
    }

    /// <summary>
    /// Ensures the data list is correctly ordered.
    /// Basically applies a reverse on the data if the Paginate direction was Backward.
    /// </summary>
    /// <typeparam name="T">The type of the entity.</typeparam>
    /// <typeparam name="T2">The type of the elements of the data.</typeparam>
    /// <param name="context">The <see cref="PaginationContext{T}"/> object.</param>
    /// <param name="data">The data list.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/> is null.
    /// <paramref name="data"/> is null.
    /// </exception>
    public static void EnsureCorrectOrder<T, T2>(
        this PaginationContext<T> context,
        IList<T2> data)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(data);

        if (context.Direction == PaginationDirection.Backward)
        {
            if (data is List<T2> list)
            {
                CollectionsMarshal.AsSpan(list).Reverse();
            }
            else
            {
                for (int i = 0, j = data.Count - 1; i < j; i++, j--)
                {
                    (data[i], data[j]) = (data[j], data[i]);
                }
            }
        }
    }

    private static Expression<Func<T, bool>> BuildFilterPredicateExpression<T>(
        PaginationColumn<T>[] columns,
        PaginationDirection direction,
        object reference)
    {
        return FilterPredicateStrategy.Default.BuildFilterPredicateExpression(
            columns,
            direction,
            reference);
    }

    private static Expression<Func<T, bool>> BuildFilterPredicateExpression<T, TReference>(
        PaginationColumn<T>[] columns,
        PaginationDirection direction,
        TReference reference)
    {
        var referenceValueBodies = new Expression[columns.Length];
        for (var i = 0; i < columns.Length; i++)
        {
            var holder = new ValueHolder { Value = columns[i].ObtainValue(reference) };
            referenceValueBodies[i] = Expression.Field(
                Expression.Constant(holder), ValueHolder.ValueField);
        }

        var param = Expression.Parameter(typeof(T), "entity");
        var finalExpression = FilterPredicateStrategy.Default.BuildExpressionCoreInternal(
            columns, direction, referenceValueBodies, param);
        return Expression.Lambda<Func<T, bool>>(finalExpression, param);
    }

    private static object?[] OrderValuesByColumns<T>(
        PaginationColumn<T>[] columns,
        ReadOnlySpan<ColumnValue> referenceValues)
    {
        var ordered = new object?[columns.Length];
        for (var i = 0; i < columns.Length; i++)
        {
            var columnName = columns[i].GetRequiredPropertyNameForColumnValues();
            var found = false;
            for (var j = 0; j < referenceValues.Length; j++)
            {
                if (string.Equals(referenceValues[j].Name, columnName, StringComparison.OrdinalIgnoreCase))
                {
                    ordered[i] = referenceValues[j].Value;
                    found = true;
                    break;
                }
            }

            if (!found)
                throw new ArgumentException(
                    $"No value provided for pagination column '{columnName}'.", nameof(referenceValues));
        }

        return ordered;
    }

    private static Expression<Func<T, bool>> BuildFilterPredicateExpressionFromValues<T>(
        PaginationColumn<T>[] columns,
        PaginationDirection direction,
        object?[] orderedValues)
    {
        var referenceValueBodies = new Expression[orderedValues.Length];
        for (var i = 0; i < orderedValues.Length; i++)
        {
            var holder = new ValueHolder { Value = orderedValues[i] };
            referenceValueBodies[i] = Expression.Field(
                Expression.Constant(holder), ValueHolder.ValueField);
        }

        var param = Expression.Parameter(typeof(T), "entity");
        var finalExpression = FilterPredicateStrategy.Default.BuildExpressionCoreInternal(
            columns, direction, referenceValueBodies, param);
        return Expression.Lambda<Func<T, bool>>(finalExpression, param);
    }

    private static Expression<Func<T, bool>> BuildFilterPredicateExpressionCached<T, TReference>(
        CachedPredicateTemplate<T> template,
        PaginationColumn<T>[] columns,
        PaginationDirection direction,
        TReference reference)
    {
        return template.Build(direction, columns, reference);
    }

    private static Expression<Func<T, bool>> BuildFilterPredicateExpressionCached<T>(
        CachedPredicateTemplate<T> template,
        PaginationColumn<T>[] columns,
        PaginationDirection direction,
        object reference)
    {
        return template.Build(direction, columns, reference);
    }
}
