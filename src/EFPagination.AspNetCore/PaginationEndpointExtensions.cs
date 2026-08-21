using System.Linq.Expressions;
using Microsoft.AspNetCore.Http;

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
    /// <exception cref="BadHttpRequestException">The request's cursor is invalid or expired, or its page size is not a positive integer of at most <paramref name="maxPageSize"/>.</exception>
    public static async Task<PaginatedResponse<TOut>> PaginateAsync<T, TOut>(
        this IQueryable<T> query,
        PaginationQueryDefinition<T> definition,
        PaginationRequest request,
        Func<T, TOut> selector,
        int maxPageSize = 100,
        bool includeCount = false,
        CancellationToken ct = default) where T : class
    {
        if (request.PageSize > maxPageSize)
            throw new BadHttpRequestException($"pageSize '{request.PageSize}' exceeds the maximum page size {maxPageSize}.");

        var (cursor, direction) = request.Before is not null
            ? (request.Before, PaginationDirection.Backward)
            : (request.After, PaginationDirection.Forward);

        try
        {
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
        catch (ArgumentException e) when (e is not ArgumentNullException)
        {
            throw new BadHttpRequestException(e.Message, e);
        }
    }

    /// <summary>
    /// Executes a paginated query from a <see cref="PaginationRequest"/> using a
    /// <see cref="PaginationSortRegistry{T}"/> to resolve dynamic sort fields, and returns a
    /// typed <see cref="PaginatedResponse{TOut}"/>. Resolves direction from
    /// <see cref="PaginationRequest.After"/> versus <see cref="PaginationRequest.Before"/>.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <typeparam name="TOut">The projected DTO type.</typeparam>
    /// <param name="query">The base <see cref="IQueryable{T}"/>.</param>
    /// <param name="registry">The sort registry that resolves <see cref="PaginationRequest.SortBy"/> to a definition.</param>
    /// <param name="request">The bound pagination parameters.</param>
    /// <param name="selector">An in-memory projection from entity to DTO, applied after materialization.</param>
    /// <param name="maxPageSize">The maximum allowed page size. Defaults to <c>100</c>.</param>
    /// <param name="includeCount">When <see langword="true"/>, computes the total row count via an additional SQL query.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A task that resolves to a <see cref="PaginatedResponse{TOut}"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="registry"/> is <see langword="null"/>.</exception>
    public static Task<PaginatedResponse<TOut>> PaginateAsync<T, TOut>(
        this IQueryable<T> query,
        PaginationSortRegistry<T> registry,
        PaginationRequest request,
        Func<T, TOut> selector,
        int maxPageSize = 100,
        bool includeCount = false,
        CancellationToken ct = default) where T : class
    {
        var definition = registry.Resolve(request);
        return PaginateAsync(query, definition, request, selector, maxPageSize, includeCount, ct);
    }

    /// <summary>
    /// Executes a paginated query from a <see cref="PaginationRequest"/> with a server-side projection
    /// and returns a typed <see cref="PaginatedResponse{TOut}"/>. The projection runs inside the SQL
    /// statement that applies the keyset ORDER BY, so subqueries inside <paramref name="selector"/>
    /// stay server-side and the <c>SELECT</c> list materializes only the projected columns.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <typeparam name="TOut">The projected DTO type.</typeparam>
    /// <param name="query">The base <see cref="IQueryable{T}"/>.</param>
    /// <param name="definition">The prebuilt pagination definition.</param>
    /// <param name="request">The bound pagination parameters.</param>
    /// <param name="selector">A server-translatable projection from entity to DTO.</param>
    /// <param name="maxPageSize">The maximum allowed page size. Defaults to <c>100</c>.</param>
    /// <param name="includeCount">When <see langword="true"/>, computes the total row count via an additional SQL query.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A task that resolves to a <see cref="PaginatedResponse{TOut}"/> with projected items.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/>, <paramref name="definition"/>, or <paramref name="selector"/> is <see langword="null"/>.</exception>
    /// <exception cref="BadHttpRequestException">The request's cursor is invalid or expired, or its page size is not a positive integer of at most <paramref name="maxPageSize"/>.</exception>
    /// <exception cref="NotSupportedException">The pagination definition has fewer than 1 or more than 8 key columns.</exception>
    public static async Task<PaginatedResponse<TOut>> PaginateAsync<T, TOut>(
        this IQueryable<T> query,
        PaginationQueryDefinition<T> definition,
        PaginationRequest request,
        Expression<Func<T, TOut>> selector,
        int maxPageSize = 100,
        bool includeCount = false,
        CancellationToken ct = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(selector);

        if (request.PageSize > maxPageSize)
            throw new BadHttpRequestException($"pageSize '{request.PageSize}' exceeds the maximum page size {maxPageSize}.");

        var builder = query.Keyset(definition).FromRequest(request).MaxPageSize(maxPageSize);
        if (includeCount)
            builder = builder.IncludeCount();

        try
        {
            var page = await builder.TakeAsync(request.PageSize, selector, ct).ConfigureAwait(false);

            return page.ToPaginatedResponse();
        }
        catch (ArgumentException e) when (e is not ArgumentNullException)
        {
            throw new BadHttpRequestException(e.Message, e);
        }
    }

    /// <summary>
    /// Executes a paginated query from a <see cref="PaginationRequest"/> using a
    /// <see cref="PaginationSortRegistry{T}"/> to resolve dynamic sort fields, with a server-side
    /// projection. The projection runs inside the SQL statement that applies the keyset ORDER BY.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <typeparam name="TOut">The projected DTO type.</typeparam>
    /// <param name="query">The base <see cref="IQueryable{T}"/>.</param>
    /// <param name="registry">The sort registry that resolves <see cref="PaginationRequest.SortBy"/> to a definition.</param>
    /// <param name="request">The bound pagination parameters.</param>
    /// <param name="selector">A server-translatable projection from entity to DTO.</param>
    /// <param name="maxPageSize">The maximum allowed page size. Defaults to <c>100</c>.</param>
    /// <param name="includeCount">When <see langword="true"/>, computes the total row count via an additional SQL query.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A task that resolves to a <see cref="PaginatedResponse{TOut}"/> with projected items.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="registry"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The resolved pagination definition has fewer than 1 or more than 8 key columns.</exception>
    public static Task<PaginatedResponse<TOut>> PaginateAsync<T, TOut>(
        this IQueryable<T> query,
        PaginationSortRegistry<T> registry,
        PaginationRequest request,
        Expression<Func<T, TOut>> selector,
        int maxPageSize = 100,
        bool includeCount = false,
        CancellationToken ct = default) where T : class
    {
        var definition = registry.Resolve(request);
        return PaginateAsync(query, definition, request, selector, maxPageSize, includeCount, ct);
    }

}
