#pragma warning disable CA1002
namespace EFPagination;

/// <summary>
/// Represents a materialized keyset-pagination page with encoded cursor tokens
/// for stateless client navigation.
/// </summary>
/// <typeparam name="T">The element type in the page.</typeparam>
/// <param name="Items">The materialized items for the current page, in correct order.</param>
/// <param name="NextCursor">An opaque cursor token for fetching the next page, or <see langword="null"/> when no more pages exist.</param>
/// <param name="PreviousCursor">An opaque cursor token for fetching the previous page, or <see langword="null"/> when on the first page.</param>
/// <param name="TotalCount">The total row count when requested; otherwise <c>-1</c>.</param>
public readonly record struct CursorPage<T>(
    List<T> Items,
    string? NextCursor,
    string? PreviousCursor,
    int TotalCount);
