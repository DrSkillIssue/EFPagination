#pragma warning disable CA1002
namespace EFPagination;

/// <summary>
/// Represents a materialized page of keyset-pagination results with full navigation metadata.
/// </summary>
/// <typeparam name="T">The element type in the page.</typeparam>
/// <param name="Items">The materialized items for the current page.</param>
/// <param name="HasPrevious"><see langword="true"/> when a previous page exists.</param>
/// <param name="HasNext"><see langword="true"/> when a next page exists after <paramref name="Items"/>.</param>
/// <param name="TotalCount">The total row count when requested; otherwise <c>-1</c>.</param>
public readonly record struct KeysetPage<T>(
    List<T> Items,
    bool HasPrevious,
    bool HasNext,
    int TotalCount = -1);
