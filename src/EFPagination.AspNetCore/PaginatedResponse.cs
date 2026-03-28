namespace EFPagination.AspNetCore;

/// <summary>
/// A JSON-serializable paginated response envelope with opaque cursor tokens.
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
/// <param name="Items">The items for the current page.</param>
/// <param name="NextCursor">The cursor token for the next page, or <see langword="null"/> when there are no more pages.</param>
/// <param name="PreviousCursor">The cursor token for the previous page, or <see langword="null"/> when on the first page.</param>
/// <param name="TotalCount">The total row count when requested, or <see langword="null"/> when not included.</param>
public sealed record PaginatedResponse<T>(
    IReadOnlyList<T> Items,
    string? NextCursor,
    string? PreviousCursor,
    int? TotalCount);
