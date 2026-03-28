namespace EFPagination;

/// <summary>
/// Factory for creating <see cref="SortField{T}"/> instances with minimal boilerplate.
/// </summary>
public static class SortField
{
    /// <summary>
    /// Creates a sort field by building both ascending and descending definitions from a property name.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="name">The external sort field name to resolve.</param>
    /// <param name="propertyName">The entity property to sort by.</param>
    /// <param name="tiebreaker">Optional tiebreaker property name. Defaults to "Id".</param>
    /// <param name="tiebreakerDescending">Whether the tiebreaker sort is descending.</param>
    /// <returns>A <see cref="SortField{T}"/> with both ascending and descending definitions.</returns>
    public static SortField<T> Create<T>(
        string name,
        string propertyName,
        string? tiebreaker = "Id",
        bool tiebreakerDescending = false)
    {
        return new SortField<T>(
            name,
            PaginationQuery.Build<T>(propertyName, descending: false, tiebreaker, tiebreakerDescending),
            PaginationQuery.Build<T>(propertyName, descending: true, tiebreaker, tiebreakerDescending));
    }
}

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
    PaginationQueryDefinition<T> Descending);
