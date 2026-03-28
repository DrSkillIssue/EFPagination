namespace EFPagination.AspNetCore;

/// <summary>
/// Extensions for integrating <see cref="KeysetQueryBuilder{T}"/> with ASP.NET Core pagination requests.
/// </summary>
public static class KeysetBuilderAspNetExtensions
{
    /// <summary>
    /// Applies cursor and direction from a <see cref="PaginationRequest"/>.
    /// <see cref="PaginationRequest.Before"/> takes precedence over <see cref="PaginationRequest.After"/>.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="builder">The keyset query builder.</param>
    /// <param name="request">The bound pagination request parameters.</param>
    /// <returns>A new builder configured from the request cursors.</returns>
    public static KeysetQueryBuilder<T> FromRequest<T>(
        this KeysetQueryBuilder<T> builder,
        PaginationRequest request) where T : class
    {
        if (request.Before is not null)
            return builder.Before(request.Before);

        if (request.After is not null)
            return builder.After(request.After);

        return builder;
    }

    /// <summary>
    /// Creates a keyset query builder from a sort registry and pagination request,
    /// resolving the definition from the request's sort parameters and applying cursors.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="source">The base <see cref="IQueryable{T}"/>.</param>
    /// <param name="registry">The sort registry for resolving the definition.</param>
    /// <param name="request">The pagination request with sort and cursor parameters.</param>
    /// <returns>A <see cref="KeysetQueryBuilder{T}"/> configured from the registry and request.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="registry"/> is <see langword="null"/>.</exception>
    public static KeysetQueryBuilder<T> Keyset<T>(
        this IQueryable<T> source,
        PaginationSortRegistry<T> registry,
        PaginationRequest request) where T : class
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(registry);

        var definition = registry.Resolve(
            request.SortBy.AsSpan(),
            request.SortDir.AsSpan());

        var builder = new KeysetQueryBuilder<T>(source, definition)
            .WithSortBy(request.SortBy);

        if (request.Before is not null)
            return builder.Before(request.Before);

        if (request.After is not null)
            return builder.After(request.After);

        return builder;
    }
}
