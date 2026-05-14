namespace EFPagination.Internal;

/// <summary>
/// Builds the <c>(next, previous)</c> cursor pair for a materialized page, encoding the trailing
/// and leading entities respectively when the corresponding cursor is meaningful.
/// </summary>
internal static class CursorPair
{
    /// <summary>
    /// Encodes the next/previous cursor pair for a materialized page.
    /// </summary>
    /// <typeparam name="T">The page item type.</typeparam>
    /// <param name="definition">The pagination definition used to encode cursor values.</param>
    /// <param name="items">The materialized page in correct order.</param>
    /// <param name="hasMore">Whether the source query has more rows beyond <paramref name="items"/>.</param>
    /// <param name="hasInitialReference">Whether the page request itself originated from a non-empty cursor or reference.</param>
    /// <param name="direction">The pagination direction.</param>
    /// <param name="sortBy">An optional logical sort key to embed in the cursor metadata.</param>
    /// <param name="totalCount">An optional total row count to embed in the cursor metadata.</param>
    /// <returns><c>(null, null)</c> when <paramref name="items"/> is empty; otherwise the cursor pair.</returns>
    public static (string? Next, string? Previous) Encode<T>(
        PaginationQueryDefinition<T> definition,
        List<T> items,
        bool hasMore,
        bool hasInitialReference,
        PaginationDirection direction,
        string? sortBy,
        int totalCount) where T : class
    {
        if (items.Count == 0) return (null, null);

        var options = new PaginationCursorOptions(
            sortBy,
            totalCount > 0 ? totalCount : null);

        var next = hasMore ? PaginationCursor.Encode(definition, items[^1], options) : null;
        var previous = (hasInitialReference || direction == PaginationDirection.Backward)
            ? PaginationCursor.Encode(definition, items[0], options)
            : null;
        return (next, previous);
    }
}
