using EFPagination.Internal;

namespace EFPagination;

/// <summary>
/// A prebuilt, reusable pagination query definition. Stores the pagination columns and a cached
/// predicate template for efficient per-call expression tree instantiation.
/// Build once via <see cref="PaginationQuery.Build{T}(Action{PaginationBuilder{T}})"/> and reuse across requests.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public sealed class PaginationQueryDefinition<T>
{
    internal PaginationQueryDefinition(
        PaginationColumn<T>[] columns)
    {
        Columns = columns;
        PredicateTemplate = FilterPredicateStrategy.Default.CreateTemplate(columns);
    }

    /// <summary>
    /// Gets the pagination columns defining the sort order.
    /// </summary>
    internal PaginationColumn<T>[] Columns { get; }

    /// <summary>
    /// Gets the cached predicate template for this definition.
    /// </summary>
    internal CachedPredicateTemplate<T> PredicateTemplate { get; }
}
