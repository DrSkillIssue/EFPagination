namespace EFPagination.AspNetCore;

/// <summary>
/// Binds cursor-based pagination parameters from the query string.
/// Supports both forward (<paramref name="After"/>) and backward (<paramref name="Before"/>) navigation.
/// When <paramref name="Before"/> is provided, the query paginates backward from that cursor.
/// When both are provided, <paramref name="Before"/> takes precedence.
/// </summary>
/// <param name="After">The opaque cursor token for forward navigation (next page), or <see langword="null"/> for the first page.</param>
/// <param name="Before">The opaque cursor token for backward navigation (previous page), or <see langword="null"/>.</param>
/// <param name="PageSize">The requested page size. Clamped by <see cref="ExecutionOptions.MaxPageSize"/>.</param>
/// <param name="SortBy">The logical sort field name, or <see langword="null"/> for the default sort.</param>
/// <param name="SortDir">The sort direction (<c>"asc"</c> or <c>"desc"</c>), or <see langword="null"/> for ascending.</param>
public readonly record struct PaginationRequest(
    string? After = null,
    string? Before = null,
    int PageSize = 25,
    string? SortBy = null,
    string? SortDir = null);
