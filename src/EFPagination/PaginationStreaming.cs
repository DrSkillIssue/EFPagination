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
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="query">The base <see cref="IQueryable{T}"/> to paginate.</param>
    /// <param name="definition">The prebuilt pagination query definition.</param>
    /// <param name="pageSize">The maximum number of items per page.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>An <see cref="IAsyncEnumerable{T}"/> that yields one <see cref="List{T}"/> per page until all rows are consumed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> or <paramref name="definition"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageSize"/> is zero or negative.</exception>
    public static async IAsyncEnumerable<List<T>> PaginateAllAsync<T>(
        IQueryable<T> query,
        PaginationQueryDefinition<T> definition,
        int pageSize,
        [EnumeratorCancellation] CancellationToken ct = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);

        object? reference = null;
        bool hasMore;

        do
        {
            var context = query.Paginate(definition, PaginationDirection.Forward, reference);
            var items = await context.Query.Take(pageSize + 1).ToListAsync(ct).ConfigureAwait(false);

            hasMore = items.Count > pageSize;
            if (hasMore)
                items.RemoveAt(items.Count - 1);

            if (items.Count == 0)
                yield break;

            reference = items[^1];
            yield return items;
        } while (hasMore);
    }
}
