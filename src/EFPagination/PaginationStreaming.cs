#pragma warning disable CA1002
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;

namespace EFPagination;

/// <summary>
/// Provides <see cref="IAsyncEnumerable{T}"/>-based streaming pagination that automatically
/// advances through all pages.
/// </summary>
public static class PaginationStreaming
{
    /// <summary>
    /// Yields successive pages by automatically advancing the keyset cursor through all matching rows.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> or <paramref name="definition"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageSize"/> is zero or negative.</exception>
    public static IAsyncEnumerable<List<T>> PaginateAllAsync<T>(
        IQueryable<T> query,
        PaginationQueryDefinition<T> definition,
        int pageSize,
        CancellationToken ct = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);

        var context = query.Paginate(definition, PaginationDirection.Forward);
        return StreamForwardAsync(query, definition, context, pageSize, ct);
    }

    internal static async IAsyncEnumerable<List<T>> StreamForwardAsync<T>(
        IQueryable<T> source,
        PaginationQueryDefinition<T> definition,
        PaginationContext<T> firstContext,
        int pageSize,
        [EnumeratorCancellation] CancellationToken ct = default) where T : class
    {
        var items = await firstContext.Query.Take(pageSize + 1).ToListAsync(ct).ConfigureAwait(false);
        var hasMore = items.Count > pageSize;
        if (hasMore) items.RemoveAt(items.Count - 1);
        if (items.Count == 0) yield break;
        object reference = items[^1]!;
        yield return items;

        while (hasMore)
        {
            var context = source.Paginate(definition, PaginationDirection.Forward, reference);
            items = await context.Query.Take(pageSize + 1).ToListAsync(ct).ConfigureAwait(false);
            hasMore = items.Count > pageSize;
            if (hasMore) items.RemoveAt(items.Count - 1);
            if (items.Count == 0) yield break;
            reference = items[^1]!;
            yield return items;
        }
    }
}
