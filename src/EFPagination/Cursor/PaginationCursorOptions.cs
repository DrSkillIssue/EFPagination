namespace EFPagination;

/// <summary>
/// Optional metadata stored alongside encoded cursor values.
/// </summary>
/// <param name="SortBy">The logical sort key associated with the cursor, or <see langword="null"/>.</param>
/// <param name="TotalCount">The total row count associated with the cursor, or <see langword="null"/>.</param>
/// <param name="SchemaFingerprint">A definition fingerprint for detecting stale cursors, or <see langword="null"/>.</param>
/// <param name="SigningKey">An HMAC-SHA256 key for cursor integrity verification, or <see langword="null"/> to skip signing.</param>
#pragma warning disable CA1819
public readonly record struct PaginationCursorOptions(
    string? SortBy = null,
    int? TotalCount = null,
    uint? SchemaFingerprint = null,
    byte[]? SigningKey = null);
