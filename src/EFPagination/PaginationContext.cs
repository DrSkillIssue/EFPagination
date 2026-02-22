using EFPagination.Internal;

namespace EFPagination;

/// <summary>
/// Contains the result of a <see cref="PaginationExtensions.Paginate{T}(IQueryable{T}, PaginationQueryDefinition{T}, PaginationDirection, object?)"/> call,
/// holding the filtered query, the ordered (unfiltered) query, and metadata needed for
/// <see cref="PaginationExtensions.HasPreviousAsync{T, T2}"/> /
/// <see cref="PaginationExtensions.HasNextAsync{T, T2}"/> checks.
/// </summary>
/// <typeparam name="T">The entity type being paginated.</typeparam>
public sealed class PaginationContext<T>
{
    internal PaginationContext(
        IQueryable<T> query,
        IOrderedQueryable<T> orderedQuery,
        PaginationColumn<T>[] columns,
        PaginationDirection direction,
        CachedPredicateTemplate<T>? predicateTemplate = null)
    {
        Query = query;
        OrderedQuery = orderedQuery;
        Columns = columns;
        Direction = direction;
        PredicateTemplate = predicateTemplate;
    }

    /// <summary>
    /// Gets the final query with both ordering and pagination filtering applied.
    /// </summary>
    public IQueryable<T> Query { get; }

    /// <summary>
    /// Gets the query with only ordering applied (no pagination filter predicate).
    /// Used internally by <c>HasPreviousAsync</c> and <c>HasNextAsync</c>.
    /// </summary>
    public IQueryable<T> OrderedQuery { get; }

    /// <summary>
    /// Gets the pagination direction that was used to create this context.
    /// </summary>
    public PaginationDirection Direction { get; }

    /// <summary>
    /// Gets the pagination columns that define the sort order for this pagination context.
    /// </summary>
    internal PaginationColumn<T>[] Columns { get; }

    /// <summary>
    /// Gets the cached predicate template, if this context was created from a <see cref="PaginationQueryDefinition{T}"/>.
    /// <see langword="null"/> for ad-hoc pagination calls.
    /// </summary>
    internal CachedPredicateTemplate<T>? PredicateTemplate { get; }
}
