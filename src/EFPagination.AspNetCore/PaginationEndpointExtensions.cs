namespace EFPagination.AspNetCore;

/// <summary>
/// Provides one-call endpoint handlers that bind pagination parameters, execute the
/// keyset query, and return a <see cref="PaginatedResponse{T}"/>.
/// </summary>
public static class PaginationEndpointExtensions
{
    /// <summary>
    /// Executes a paginated query from a <see cref="PaginationRequest"/> and returns a typed <see cref="PaginatedResponse{TOut}"/>.
    /// Resolves direction from <see cref="PaginationRequest.After"/> vs <see cref="PaginationRequest.Before"/>.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <typeparam name="TOut">The projected DTO type.</typeparam>
    /// <param name="query">The base <see cref="IQueryable{T}"/>.</param>
    /// <param name="definition">The prebuilt pagination definition.</param>
    /// <param name="request">The bound pagination parameters.</param>
    /// <param name="selector">A projection from entity to DTO.</param>
    /// <param name="maxPageSize">The maximum allowed page size. Defaults to 100.</param>
    /// <param name="includeCount">Whether to compute the total row count.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A task that resolves to a <see cref="PaginatedResponse{TOut}"/>.</returns>
    public static async Task<PaginatedResponse<TOut>> PaginateAsync<T, TOut>(
        this IQueryable<T> query,
        PaginationQueryDefinition<T> definition,
        PaginationRequest request,
        Func<T, TOut> selector,
        int maxPageSize = 100,
        bool includeCount = false,
        CancellationToken ct = default) where T : class
    {
        var (cursor, direction) = ResolveCursorAndDirection(request);

        var page = await PaginationExecutor.ExecuteFromCursorAsync(
            query,
            definition,
            new ExecutionOptions(
                PageSize: request.PageSize,
                Direction: direction,
                IncludeCount: includeCount,
                MaxPageSize: maxPageSize),
            cursor,
            ct).ConfigureAwait(false);

        return page.ToPaginatedResponse(selector);
    }

    /// <summary>
    /// Executes a paginated query from a <see cref="PaginationRequest"/> using a
    /// <see cref="PaginationSortRegistry{T}"/> to resolve dynamic sort fields,
    /// and returns a typed <see cref="PaginatedResponse{TOut}"/>.
    /// Resolves direction from <see cref="PaginationRequest.After"/> vs <see cref="PaginationRequest.Before"/>.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <typeparam name="TOut">The projected DTO type.</typeparam>
    /// <param name="query">The base <see cref="IQueryable{T}"/>.</param>
    /// <param name="registry">The sort registry to resolve <see cref="PaginationRequest.SortBy"/>.</param>
    /// <param name="request">The bound pagination parameters.</param>
    /// <param name="selector">A projection from entity to DTO.</param>
    /// <param name="maxPageSize">The maximum allowed page size. Defaults to 100.</param>
    /// <param name="includeCount">Whether to compute the total row count.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A task that resolves to a <see cref="PaginatedResponse{TOut}"/>.</returns>
    public static async Task<PaginatedResponse<TOut>> PaginateAsync<T, TOut>(
        this IQueryable<T> query,
        PaginationSortRegistry<T> registry,
        PaginationRequest request,
        Func<T, TOut> selector,
        int maxPageSize = 100,
        bool includeCount = false,
        CancellationToken ct = default) where T : class
    {
        var definition = registry.Resolve(
            request.SortBy.AsSpan(),
            request.SortDir.AsSpan());

        var (cursor, direction) = ResolveCursorAndDirection(request);

        var page = await PaginationExecutor.ExecuteFromCursorAsync(
            query,
            definition,
            new ExecutionOptions(
                PageSize: request.PageSize,
                Direction: direction,
                IncludeCount: includeCount,
                MaxPageSize: maxPageSize),
            cursor,
            ct).ConfigureAwait(false);

        return page.ToPaginatedResponse(selector);
    }

    private static (string? cursor, PaginationDirection direction) ResolveCursorAndDirection(PaginationRequest request)
    {
        if (request.Before is not null)
            return (request.Before, PaginationDirection.Backward);

        return (request.After, PaginationDirection.Forward);
    }
}
