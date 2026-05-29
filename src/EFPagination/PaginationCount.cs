namespace EFPagination;

/// <summary>
/// A page's total row count when no count was requested, and reading that as <see langword="null"/>.
/// </summary>
public static class PaginationCount
{
    /// <summary>
    /// The total row count a page reports when no count was requested.
    /// </summary>
    public const int None = -1;

    /// <summary>
    /// Reads a total row count as <see langword="null"/> when no count is present.
    /// </summary>
    /// <param name="count">A total row count, or <see cref="None"/> when no count was requested.</param>
    /// <returns>The count, or <see langword="null"/> when no count is present.</returns>
    public static int? AsNullable(int count) => count < 0 ? null : count;
}
