namespace EFPagination;

/// <summary>
/// Maps a logical sort field name to prebuilt ascending and descending pagination definitions.
/// </summary>
/// <typeparam name="T">The entity type for the definitions.</typeparam>
/// <param name="Name">The external sort field name to resolve.</param>
/// <param name="Ascending">The pagination definition used for ascending requests.</param>
/// <param name="Descending">The pagination definition used for descending requests.</param>
public readonly record struct SortField<T>(
    string Name,
    PaginationQueryDefinition<T> Ascending,
    PaginationQueryDefinition<T> Descending) where T : class;
