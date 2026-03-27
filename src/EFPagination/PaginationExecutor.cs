#pragma warning disable CS1591
#pragma warning disable CA1002 // List<T> in public API: intentional — avoids IReadOnlyList<T> interface dispatch and List<T>.Enumerator boxing on consumer foreach
using Microsoft.EntityFrameworkCore;

namespace EFPagination;

public static class PaginationExecutor
{
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
