using Microsoft.EntityFrameworkCore;

namespace EFPagination.Internal;

/// <summary>
/// Single source of truth for deciding a page's total row count. A count carried by an inbound
/// cursor is reused verbatim; a fresh <c>COUNT(*)</c> is issued only when a count was requested
/// and none was carried — that is, on a cursor-less, first-of-chain request. Subsequent pages in
/// the same cursor chain inherit the original count without re-querying.
/// </summary>
internal static class TotalCountResolver
{
    /// <summary>
    /// Resolves the total row count for a page.
    /// </summary>
    /// <typeparam name="T">The source element type.</typeparam>
    /// <param name="includeCount">Whether the caller requested a total count.</param>
    /// <param name="cachedCount">The count carried by the inbound cursor, or <see langword="null"/> when absent.</param>
    /// <param name="source">The unpaginated source query used to compute a fresh count.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>
    /// The carried count when present; otherwise a fresh count when requested; otherwise
    /// <see cref="PaginationCount.None"/>.
    /// </returns>
    public static Task<int> ResolveAsync<T>(
        bool includeCount,
        int? cachedCount,
        IQueryable<T> source,
        CancellationToken ct)
    {
        if (cachedCount is int carried)
            return Task.FromResult(carried);

        return includeCount
            ? source.CountAsync(ct)
            : Task.FromResult(PaginationCount.None);
    }
}
