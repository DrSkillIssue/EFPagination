namespace EFPagination;

/// <summary>
/// Optional metadata recovered alongside the values when a cursor is decoded.
/// </summary>
/// <param name="SortBy">The logical sort key embedded in the cursor, or <see langword="null"/> when absent.</param>
/// <param name="TotalCount">The total row count embedded in the cursor, or <see langword="null"/> when absent.</param>
/// <param name="Fingerprint">The schema fingerprint embedded in the cursor, or <see langword="null"/> when absent.</param>
/// <param name="ValueCount">The number of boundary values decoded.</param>
public readonly record struct CursorMetadata(
    string? SortBy,
    int? TotalCount,
    uint? Fingerprint,
    int ValueCount);
