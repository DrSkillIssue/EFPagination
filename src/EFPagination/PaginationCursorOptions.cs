namespace EFPagination;

/// <summary>
/// Optional metadata stored alongside encoded cursor values.
/// </summary>
/// <param name="SortBy">The logical sort key associated with the cursor, or <see langword="null"/>.</param>
/// <param name="TotalCount">The total row count associated with the cursor, or <see langword="null"/>.</param>
public readonly record struct PaginationCursorOptions(string? SortBy = null, int? TotalCount = null);
