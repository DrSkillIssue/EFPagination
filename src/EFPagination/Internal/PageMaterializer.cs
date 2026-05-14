using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;

namespace EFPagination.Internal;

internal static class PageMaterializer
{
    /// <summary>
    /// Materializes a single page using the take-(pageSize+1) overflow pattern. Trims the
    /// extra item, reverses the list in-place when the direction is
    /// <see cref="PaginationDirection.Backward"/>, and reports whether more items existed.
    /// </summary>
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
