using EFPagination.Internal;

namespace EFPagination;

/// <summary>
/// Contains the result of a <c>Paginate</c> call, holding the filtered query, the ordered
/// (unfiltered) query, and metadata needed for follow-up operations.
/// </summary>
/// <typeparam name="T">The entity type being paginated.</typeparam>
#pragma warning disable CA1815
public readonly struct PaginationContext<T>
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
    /// </summary>
    public IQueryable<T> OrderedQuery { get; }

    /// <summary>
    /// Gets the pagination direction that was used to create this context.
    /// </summary>
    public PaginationDirection Direction { get; }

    internal PaginationColumn<T>[] Columns { get; }

    internal CachedPredicateTemplate<T>? PredicateTemplate { get; }
}
