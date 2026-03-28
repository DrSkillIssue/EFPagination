namespace EFPagination;

/// <summary>
/// Provides the <see cref="Keyset{T}"/> entry point for fluent keyset pagination.
/// </summary>
public static class KeysetPaginationExtensions
{
    /// <summary>
    /// Begins a fluent keyset pagination chain over the source query.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="source">The base <see cref="IQueryable{T}"/> to paginate.</param>
    /// <param name="definition">The prebuilt pagination query definition.</param>
    /// <returns>A <see cref="KeysetQueryBuilder{T}"/> for chaining pagination parameters.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="definition"/> is <see langword="null"/>.</exception>
    public static KeysetQueryBuilder<T> Keyset<T>(
        this IQueryable<T> source,
        PaginationQueryDefinition<T> definition) where T : class
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(definition);
        return new KeysetQueryBuilder<T>(source, definition);
    }
}
