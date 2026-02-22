using System.Linq.Expressions;
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

    private static Task<bool> HasAsync<T>(
        this PaginationContext<T> context,
        PaginationDirection direction,
        object reference)
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
            for (int i = 0, j = data.Count - 1; i < j; i++, j--)
            {
                (data[i], data[j]) = (data[j], data[i]);
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

    private static Expression<Func<T, bool>> BuildFilterPredicateExpressionCached<T>(
        CachedPredicateTemplate<T> template,
        PaginationColumn<T>[] columns,
        PaginationDirection direction,
        object reference)
    {
        var referenceValues = FilterPredicateStrategy.GetValues(columns, reference);
        return template.Build(direction, referenceValues);
    }
}
