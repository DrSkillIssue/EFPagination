using System.Runtime.InteropServices;

namespace EFPagination.AspNetCore;

/// <summary>
/// Extension methods for converting EFPagination results to ASP.NET Core response types.
/// </summary>
public static class PaginationResponseExtensions
{
    /// <summary>
    /// Converts a <see cref="CursorPage{T}"/> to a <see cref="PaginatedResponse{T}"/>
    /// suitable for JSON serialization.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="page">The cursor page to convert.</param>
    /// <returns>A response envelope with cursor tokens and optional total count.</returns>
    public static PaginatedResponse<T> ToPaginatedResponse<T>(this CursorPage<T> page)
        => new(page.Items, page.NextCursor, page.PreviousCursor,
               page.TotalCount >= 0 ? page.TotalCount : null);

    /// <summary>
    /// Converts a <see cref="CursorPage{T}"/> to a <see cref="PaginatedResponse{TOut}"/>
    /// by projecting each item through a selector.
    /// </summary>
    /// <typeparam name="T">The source entity type.</typeparam>
    /// <typeparam name="TOut">The projected DTO type.</typeparam>
    /// <param name="page">The cursor page to convert.</param>
    /// <param name="selector">A transform applied to each item.</param>
    /// <returns>A response envelope with projected items, cursor tokens, and optional total count.</returns>
    public static PaginatedResponse<TOut> ToPaginatedResponse<T, TOut>(
        this CursorPage<T> page,
        Func<T, TOut> selector)
    {
        var span = CollectionsMarshal.AsSpan(page.Items);
        var items = new TOut[span.Length];
        for (var i = 0; i < span.Length; i++)
            items[i] = selector(span[i]);

        return new PaginatedResponse<TOut>(items, page.NextCursor, page.PreviousCursor,
            page.TotalCount >= 0 ? page.TotalCount : null);
    }
}
