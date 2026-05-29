using EFPagination.Internal;

namespace EFPagination;

/// <summary>
/// Controls how <see cref="PaginationExecutor"/> materializes a page.
/// </summary>
/// <param name="PageSize">The requested number of items per page.</param>
/// <param name="Direction">The pagination direction. Defaults to <see cref="PaginationDirection.Forward"/>.</param>
/// <param name="IncludeCount">
/// When <see langword="true"/>, the page carries the total row count, computed once on a cursor-less
/// request and carried forward through later cursors. Defaults to <see langword="false"/>.
/// </param>
/// <param name="MaxPageSize">The upper bound that clamps <paramref name="PageSize"/>. Defaults to <c>500</c>.</param>
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
    /// <summary>
    /// Executes a page query using definition-bound ordered values.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="query">The base <see cref="IQueryable{T}"/> to paginate.</param>
    /// <param name="definition">The prebuilt pagination query definition.</param>
    /// <param name="options">Options controlling page size, direction, and optional total count.</param>
    /// <param name="referenceValues">The definition-bound ordered values to use as the page boundary.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A task that resolves to a <see cref="KeysetPage{T}"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> or <paramref name="definition"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="ExecutionOptions.PageSize"/> is zero or negative.</exception>
    public static Task<KeysetPage<T>> ExecuteAsync<T>(
        IQueryable<T> query,
        PaginationQueryDefinition<T> definition,
        ExecutionOptions options,
        PaginationValues<T> referenceValues,
        CancellationToken ct = default) where T : class
        => ExecuteCoreAsync(query, options, query.Paginate(definition, options.Direction, referenceValues), ct);

    /// <summary>
    /// Executes a page query using manual column name/value pairs.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="query">The base <see cref="IQueryable{T}"/> to paginate.</param>
    /// <param name="definition">The prebuilt pagination query definition.</param>
    /// <param name="options">Options controlling page size, direction, and optional total count.</param>
    /// <param name="referenceValues">The column name/value pairs to use as the page boundary.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A task that resolves to a <see cref="KeysetPage{T}"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> or <paramref name="definition"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="ExecutionOptions.PageSize"/> is zero or negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="referenceValues"/> is missing a required column.</exception>
    public static Task<KeysetPage<T>> ExecuteAsync<T>(
        IQueryable<T> query,
        PaginationQueryDefinition<T> definition,
        ExecutionOptions options,
        ReadOnlySpan<ColumnValue> referenceValues,
        CancellationToken ct = default) where T : class
        => ExecuteCoreAsync(query, options, query.Paginate(definition, options.Direction, referenceValues), ct);

    /// <summary>
    /// Executes a page query using a reference object whose properties match the pagination definition.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="query">The base <see cref="IQueryable{T}"/> to paginate.</param>
    /// <param name="definition">The prebuilt pagination query definition.</param>
    /// <param name="options">Options controlling page size, direction, and optional total count.</param>
    /// <param name="reference">The reference object, or <see langword="null"/> for the first page.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A task that resolves to a <see cref="KeysetPage{T}"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> or <paramref name="definition"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="ExecutionOptions.PageSize"/> is zero or negative.</exception>
    /// <exception cref="IncompatibleReferenceException"><paramref name="reference"/> is missing a required property.</exception>
    public static Task<KeysetPage<T>> ExecuteAsync<T>(
        IQueryable<T> query,
        PaginationQueryDefinition<T> definition,
        ExecutionOptions options,
        object? reference = null,
        CancellationToken ct = default) where T : class
        => ExecuteCoreAsync(query, options, query.Paginate(definition, options.Direction, reference), ct);

    /// <summary>
    /// Decodes an opaque cursor, executes the paginated query, and encodes next/previous cursors.
    /// </summary>
    /// <remarks>
    /// A cursor is bound to the query that produced it; reusing it after the filter changes resumes
    /// from the encoded sort position within the new result set and reports the original count. Issue
    /// a cursor-less request to restart and recompute when the query changes.
    /// </remarks>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="query">The base <see cref="IQueryable{T}"/> to paginate.</param>
    /// <param name="definition">The prebuilt pagination query definition.</param>
    /// <param name="options">Options controlling page size, direction, and optional total count.</param>
    /// <param name="cursor">The opaque cursor token, or an empty span for the first page.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A task that resolves to a <see cref="CursorPage{T}"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> or <paramref name="definition"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="ExecutionOptions.PageSize"/> is zero or negative.</exception>
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

    /// <summary>
    /// Decodes an opaque cursor string, executes the paginated query, and encodes next/previous cursors.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="query">The base <see cref="IQueryable{T}"/> to paginate.</param>
    /// <param name="definition">The prebuilt pagination query definition.</param>
    /// <param name="options">Options controlling page size, direction, and optional total count.</param>
    /// <param name="cursor">The opaque cursor string, or <see langword="null"/> for the first page.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A task that resolves to a <see cref="CursorPage{T}"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> or <paramref name="definition"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="ExecutionOptions.PageSize"/> is zero or negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="cursor"/> is invalid or expired.</exception>
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
        var (items, hasMore, totalCount) = await MaterializeWithCountAsync(
            query, options, context, previousTotalCount, ct).ConfigureAwait(false);

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

        var (items, hasMore, totalCount) = await MaterializeWithCountAsync(
            query, options, context, previousTotalCount: null, ct).ConfigureAwait(false);

        var isFiltered = !ReferenceEquals(context.Query, context.OrderedQuery);
        var hasPrevious = context.Direction == PaginationDirection.Forward ? isFiltered : hasMore;
        var hasNext = context.Direction == PaginationDirection.Forward ? hasMore : isFiltered;

        return new KeysetPage<T>(items, hasPrevious, hasNext, totalCount);
    }

    private static async Task<(List<T> Items, bool HasMore, int TotalCount)> MaterializeWithCountAsync<T>(
        IQueryable<T> query,
        ExecutionOptions options,
        PaginationContext<T> context,
        int? previousTotalCount,
        CancellationToken ct) where T : class
    {
        var (items, hasMore) = await PageMaterializer.MaterializeAsync(
            context.Query, options.EffectivePageSize, context.Direction, ct).ConfigureAwait(false);

        var totalCount = await TotalCountResolver
            .ResolveAsync(options.IncludeCount, previousTotalCount, query, ct)
            .ConfigureAwait(false);

        return (items, hasMore, totalCount);
    }
}
