using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;

namespace EFPagination.Internal;

/// <summary>
/// Materializes pages from an EF Core query using the <c>pageSize + 1</c> overflow pattern.
/// </summary>
internal static class PageMaterializer
{
    /// <summary>
    /// Materializes a single page from <paramref name="query"/>. Issues one round trip that
    /// fetches up to <c>pageSize + 1</c> rows; trims the extra row when present, reverses the
    /// list in-place when <paramref name="direction"/> is <see cref="PaginationDirection.Backward"/>,
    /// and reports whether more rows existed past the page boundary.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="query">The ordered, optionally filtered query.</param>
    /// <param name="pageSize">The page size requested by the caller.</param>
    /// <param name="direction">The pagination direction.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A tuple of the materialized items (in correct order) and a <c>hasMore</c> flag.</returns>
    public static async Task<(List<T> Items, bool HasMore)> MaterializeAsync<T>(
        IQueryable<T> query,
        int pageSize,
        PaginationDirection direction,
        CancellationToken ct)
    {
        var items = await query.Take(pageSize + 1).ToListAsync(ct).ConfigureAwait(false);
        var hasMore = items.Count > pageSize;
        if (hasMore)
            items.RemoveAt(items.Count - 1);
        if (direction == PaginationDirection.Backward)
            CollectionsMarshal.AsSpan(items).Reverse();
        return (items, hasMore);
    }
}
