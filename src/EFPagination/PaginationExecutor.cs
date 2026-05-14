using EFPagination.Internal;
using Microsoft.EntityFrameworkCore;

namespace EFPagination;

/// <summary>
/// Controls how <see cref="PaginationExecutor"/> materializes a page.
/// </summary>
/// <param name="PageSize">The requested number of items per page.</param>
/// <param name="Direction">The pagination direction. Defaults to <see cref="PaginationDirection.Forward"/>.</param>
/// <param name="IncludeCount">When <see langword="true"/>, a total row count is computed via an additional query.</param>
/// <param name="MaxPageSize">The upper bound that clamps <paramref name="PageSize"/>. Defaults to 500.</param>
public readonly record struct ExecutionOptions(
    int PageSize,
    PaginationDirection Direction = PaginationDirection.Forward,
    bool IncludeCount = false,
    int MaxPageSize = 500)
{
    internal int EffectivePageSize => PageSize > MaxPageSize ? MaxPageSize : PageSize;
}

/// <summary>
/// Executes materialized keyset-pagination queries and returns page metadata.
/// </summary>
public static class PaginationExecutor
{
    /// <summary>Executes a page query using definition-bound ordered values.</summary>
    public static Task<KeysetPage<T>> ExecuteAsync<T>(
        IQueryable<T> query,
        PaginationQueryDefinition<T> definition,
        ExecutionOptions options,
        PaginationValues<T> referenceValues,
        CancellationToken ct = default) where T : class
        => ExecuteCoreAsync(query, options, query.Paginate(definition, options.Direction, referenceValues), ct);

    /// <summary>Executes a page query using manual name/value pairs.</summary>
    public static Task<KeysetPage<T>> ExecuteAsync<T>(
        IQueryable<T> query,
        PaginationQueryDefinition<T> definition,
        ExecutionOptions options,
        ReadOnlySpan<ColumnValue> referenceValues,
        CancellationToken ct = default) where T : class
        => ExecuteCoreAsync(query, options, query.Paginate(definition, options.Direction, referenceValues), ct);

    /// <summary>Executes a page query using a reference object whose properties match the pagination definition.</summary>
    public static Task<KeysetPage<T>> ExecuteAsync<T>(
        IQueryable<T> query,
        PaginationQueryDefinition<T> definition,
        ExecutionOptions options,
        object? reference = null,
        CancellationToken ct = default) where T : class
        => ExecuteCoreAsync(query, options, query.Paginate(definition, options.Direction, reference), ct);

    /// <summary>Decodes an opaque cursor, executes the paginated query, and encodes next/previous cursors.</summary>
    /// <exception cref="ArgumentException"><paramref name="cursor"/> is invalid or expired.</exception>
    public static Task<CursorPage<T>> ExecuteFromCursorAsync<T>(
        IQueryable<T> query,
        PaginationQueryDefinition<T> definition,
        ExecutionOptions options,
        ReadOnlySpan<char> cursor,
        CancellationToken ct = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.PageSize);

        string? sortBy = null;
        int? previousTotalCount = null;
        var hasCursor = !cursor.IsEmpty;
        PaginationContext<T> context;

        if (hasCursor)
        {
            if (!PaginationCursor.TryDecode(cursor, definition, out var values, out var metadata))
                throw new ArgumentException("Invalid or expired cursor.", nameof(cursor));

            sortBy = metadata.SortBy;
            previousTotalCount = metadata.TotalCount;
            context = query.Paginate(definition, options.Direction, values);
        }
        else
        {
            context = query.Paginate(definition, options.Direction);
        }

        return ExecuteFromCursorCoreAsync(query, definition, options, context, hasCursor, sortBy, previousTotalCount, ct);
    }

    /// <summary>String overload of <see cref="ExecuteFromCursorAsync{T}(IQueryable{T}, PaginationQueryDefinition{T}, ExecutionOptions, ReadOnlySpan{char}, CancellationToken)"/>.</summary>
    public static Task<CursorPage<T>> ExecuteFromCursorAsync<T>(
        IQueryable<T> query,
        PaginationQueryDefinition<T> definition,
        ExecutionOptions options,
        string? cursor,
        CancellationToken ct = default) where T : class
        => ExecuteFromCursorAsync(query, definition, options, cursor.AsSpan(), ct);

    private static async Task<CursorPage<T>> ExecuteFromCursorCoreAsync<T>(
        IQueryable<T> query,
        PaginationQueryDefinition<T> definition,
        ExecutionOptions options,
        PaginationContext<T> context,
        bool hasCursor,
        string? sortBy,
        int? previousTotalCount,
        CancellationToken ct) where T : class
    {
        var (items, hasMore) = await PageMaterializer.MaterializeAsync(
            context.Query, options.EffectivePageSize, options.Direction, ct).ConfigureAwait(false);

        var totalCount = options.IncludeCount
            ? await query.CountAsync(ct).ConfigureAwait(false)
            : previousTotalCount ?? -1;

        var (next, previous) = CursorPair.Encode(
            definition, items, hasMore, hasCursor, options.Direction, sortBy, totalCount);

        return new CursorPage<T>(items, next, previous, totalCount);
    }

    private static async Task<KeysetPage<T>> ExecuteCoreAsync<T>(
        IQueryable<T> query,
        ExecutionOptions options,
        PaginationContext<T> context,
        CancellationToken ct) where T : class
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.PageSize);

        var (items, hasMore) = await PageMaterializer.MaterializeAsync(
            context.Query, options.EffectivePageSize, context.Direction, ct).ConfigureAwait(false);

        var isFiltered = !ReferenceEquals(context.Query, context.OrderedQuery);
        var hasPrevious = context.Direction == PaginationDirection.Forward ? isFiltered : hasMore;
        var hasNext = context.Direction == PaginationDirection.Forward ? hasMore : isFiltered;

        var totalCount = options.IncludeCount
            ? await query.CountAsync(ct).ConfigureAwait(false)
            : -1;

        return new KeysetPage<T>(items, hasPrevious, hasNext, totalCount);
    }
}
