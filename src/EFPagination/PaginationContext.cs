using EFPagination.Internal;

namespace EFPagination;

/// <summary>
/// The result of a <c>Paginate</c> call. Holds the filtered query, the ordered (unfiltered)
/// query, and metadata required for follow-up operations such as
/// <see cref="PaginationExtensions.HasPreviousAsync{T,T2}"/>,
/// <see cref="PaginationExtensions.HasNextAsync{T,T2}"/>, and
/// <see cref="PaginationExtensions.MaterializeAsync{T}"/>.
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
    /// Gets the final query with both ordering and the keyset filter predicate applied.
    /// Enumerate this to fetch the page rows.
    /// </summary>
    public IQueryable<T> Query { get; }

    /// <summary>
    /// Gets the query with only ordering applied (no keyset filter predicate). Used internally
    /// by <see cref="PaginationExtensions.HasPreviousAsync{T,T2}"/> and
    /// <see cref="PaginationExtensions.HasNextAsync{T,T2}"/> to look beyond the current page.
    /// </summary>
    public IQueryable<T> OrderedQuery { get; }

    /// <summary>
    /// Gets the <see cref="PaginationDirection"/> that was used to create this context.
    /// </summary>
    public PaginationDirection Direction { get; }

    internal PaginationColumn<T>[] Columns { get; }

    internal CachedPredicateTemplate<T>? PredicateTemplate { get; }
}
