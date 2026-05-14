namespace EFPagination.Internal;

internal static class CursorPair
{
    /// <summary>
    /// Encodes the next/previous cursor pair for a materialized page. Returns
    /// <c>(null, null)</c> when <paramref name="items"/> is empty.
    /// </summary>
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
