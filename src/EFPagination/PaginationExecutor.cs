#pragma warning disable CA1002 // List<T> in public API: intentional — avoids IReadOnlyList<T> interface dispatch and List<T>.Enumerator boxing on consumer foreach
using Microsoft.EntityFrameworkCore;

namespace EFPagination;

/// <summary>
/// Executes materialized keyset-pagination queries and returns page metadata.
/// </summary>
public static class PaginationExecutor
{
    /// <summary>
    /// Executes a forward page query using definition-bound ordered values.
    /// </summary>
    /// <typeparam name="T">The entity type being paginated.</typeparam>
    /// <param name="query">The base query to paginate.</param>
    /// <param name="definition">The pagination definition to apply.</param>
    /// <param name="pageSize">The maximum number of items to return.</param>
    /// <param name="includeCount"><see langword="true"/> to execute a total-count query; otherwise <see langword="false"/>.</param>
    /// <param name="referenceValues">The ordered boundary values for the page reference.</param>
    /// <param name="ct">The cancellation token used for query execution.</param>
    /// <returns>A task that resolves to the materialized keyset page.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/>, <paramref name="definition"/>, or <paramref name="referenceValues"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageSize"/> is less than or equal to zero.</exception>
    public static Task<KeysetPage<T>> ExecuteAsync<T>(
        IQueryable<T> query,
        PaginationQueryDefinition<T> definition,
        int pageSize,
        bool includeCount,
        PaginationValues<T> referenceValues,
        CancellationToken ct = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(referenceValues);

        return ExecuteCoreAsync(
            query,
            definition,
            pageSize,
            query.Paginate(definition, PaginationDirection.Forward, referenceValues),
            includeCount,
            ct);
    }

    /// <summary>
    /// Executes a forward page query using manual name/value pairs.
    /// </summary>
    /// <typeparam name="T">The entity type being paginated.</typeparam>
    /// <param name="query">The base query to paginate.</param>
    /// <param name="definition">The pagination definition to apply.</param>
    /// <param name="pageSize">The maximum number of items to return.</param>
    /// <param name="includeCount"><see langword="true"/> to execute a total-count query; otherwise <see langword="false"/>.</param>
    /// <param name="referenceValues">The manual column values used as the page reference.</param>
    /// <param name="ct">The cancellation token used for query execution.</param>
    /// <returns>A task that resolves to the materialized keyset page.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> or <paramref name="definition"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageSize"/> is less than or equal to zero.</exception>
    /// <exception cref="ArgumentException"><paramref name="referenceValues"/> is missing a required column when values are provided out of order.</exception>
    /// <exception cref="InvalidOperationException">A direct-value path targets a definition that cannot be addressed by column name.</exception>
    public static Task<KeysetPage<T>> ExecuteAsync<T>(
        IQueryable<T> query,
        PaginationQueryDefinition<T> definition,
        int pageSize,
        bool includeCount,
        ReadOnlySpan<ColumnValue> referenceValues,
        CancellationToken ct = default) where T : class
    {
        return ExecuteCoreAsync(
            query,
            definition,
            pageSize,
            query.Paginate(definition, PaginationDirection.Forward, referenceValues),
            includeCount,
            ct);
    }

    /// <summary>
    /// Executes a forward page query using a reference object whose properties match the pagination definition.
    /// </summary>
    /// <typeparam name="T">The entity type being paginated.</typeparam>
    /// <param name="query">The base query to paginate.</param>
    /// <param name="definition">The pagination definition to apply.</param>
    /// <param name="pageSize">The maximum number of items to return.</param>
    /// <param name="reference">The reference object used to build the keyset predicate, or <see langword="null"/> for the first page.</param>
    /// <param name="includeCount"><see langword="true"/> to execute a total-count query; otherwise <see langword="false"/>.</param>
    /// <param name="ct">The cancellation token used for query execution.</param>
    /// <returns>A task that resolves to the materialized keyset page.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> or <paramref name="definition"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageSize"/> is less than or equal to zero.</exception>
    /// <exception cref="IncompatibleReferenceException"><paramref name="reference"/> is missing a property required by <paramref name="definition"/>.</exception>
    public static Task<KeysetPage<T>> ExecuteAsync<T>(
        IQueryable<T> query,
        PaginationQueryDefinition<T> definition,
        int pageSize,
        object? reference,
        bool includeCount,
        CancellationToken ct = default) where T : class
    {
        return ExecuteCoreAsync(
            query,
            definition,
            pageSize,
            query.Paginate(definition, PaginationDirection.Forward, reference),
            includeCount,
            ct);
    }

    private static async Task<KeysetPage<T>> ExecuteCoreAsync<T>(
        IQueryable<T> query,
        PaginationQueryDefinition<T> definition,
        int pageSize,
        PaginationContext<T> context,
        bool includeCount,
        CancellationToken ct) where T : class
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);

        var items = new List<T>(pageSize + 1);
        await foreach (var item in context.Query
            .Take(pageSize + 1)
            .AsAsyncEnumerable()
            .WithCancellation(ct)
            .ConfigureAwait(false))
        {
            items.Add(item);
        }

        var hasMore = items.Count > pageSize;
        if (hasMore)
            items.RemoveAt(items.Count - 1);

        var totalCount = -1;
        if (includeCount)
        {
            totalCount = await query.CountAsync(ct).ConfigureAwait(false);
        }

        return new KeysetPage<T>(items, hasMore, totalCount);
    }
}
