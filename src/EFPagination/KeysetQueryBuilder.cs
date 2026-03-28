#pragma warning disable CA1002
#pragma warning disable CA1815

namespace EFPagination;

/// <summary>
/// A zero-allocation fluent builder for keyset pagination.
/// Accumulates pagination parameters and executes via <see cref="TakeAsync"/> or <see cref="StreamAsync"/>.
/// </summary>
/// <typeparam name="T">The entity type being paginated.</typeparam>
public readonly struct KeysetQueryBuilder<T> where T : class
{
    private const int DefaultMaxPageSize = 500;

    internal readonly IQueryable<T> Source;
    internal readonly PaginationQueryDefinition<T> Definition;
    internal readonly string? CursorString;
    internal readonly object? Reference;
    internal readonly PaginationValues<T>? BoundValues;
    internal readonly PaginationDirection Direction;
    internal readonly bool ShouldIncludeCount;
    internal readonly int MaxPageSizeValue;
    internal readonly string? SortBy;

    internal KeysetQueryBuilder(IQueryable<T> source, PaginationQueryDefinition<T> definition)
    {
        Source = source;
        Definition = definition;
        MaxPageSizeValue = DefaultMaxPageSize;
    }

    private KeysetQueryBuilder(
        IQueryable<T> source,
        PaginationQueryDefinition<T> definition,
        string? cursorString,
        object? reference,
        PaginationValues<T>? boundValues,
        PaginationDirection direction,
        bool shouldIncludeCount,
        int maxPageSizeValue,
        string? sortBy)
    {
        Source = source;
        Definition = definition;
        CursorString = cursorString;
        Reference = reference;
        BoundValues = boundValues;
        Direction = direction;
        ShouldIncludeCount = shouldIncludeCount;
        MaxPageSizeValue = maxPageSizeValue;
        SortBy = sortBy;
    }

    /// <summary>
    /// Sets the forward cursor. Items after this cursor position are returned.
    /// </summary>
    /// <param name="cursor">An opaque cursor string from a previous <see cref="CursorPage{T}.NextCursor"/>.</param>
    /// <returns>A new builder configured for forward pagination.</returns>
    public KeysetQueryBuilder<T> After(string cursor)
        => new(Source, Definition, cursor, null, null, PaginationDirection.Forward, ShouldIncludeCount, MaxPageSizeValue, SortBy);

    /// <summary>
    /// Sets the backward cursor. Items before this cursor position are returned.
    /// </summary>
    /// <param name="cursor">An opaque cursor string from a previous <see cref="CursorPage{T}.PreviousCursor"/>.</param>
    /// <returns>A new builder configured for backward pagination.</returns>
    public KeysetQueryBuilder<T> Before(string cursor)
        => new(Source, Definition, cursor, null, null, PaginationDirection.Backward, ShouldIncludeCount, MaxPageSizeValue, SortBy);

    /// <summary>
    /// Sets an entity as the forward boundary. Items after this entity are returned.
    /// The entity must have properties matching the pagination definition columns.
    /// </summary>
    /// <param name="entity">The reference entity whose property values define the cursor position.</param>
    /// <returns>A new builder configured for forward pagination.</returns>
    public KeysetQueryBuilder<T> AfterEntity(object entity)
        => new(Source, Definition, null, entity, null, PaginationDirection.Forward, ShouldIncludeCount, MaxPageSizeValue, SortBy);

    /// <summary>
    /// Sets an entity as the backward boundary. Items before this entity are returned.
    /// The entity must have properties matching the pagination definition columns.
    /// </summary>
    /// <param name="entity">The reference entity whose property values define the cursor position.</param>
    /// <returns>A new builder configured for backward pagination.</returns>
    public KeysetQueryBuilder<T> BeforeEntity(object entity)
        => new(Source, Definition, null, entity, null, PaginationDirection.Backward, ShouldIncludeCount, MaxPageSizeValue, SortBy);

    /// <summary>
    /// Sets definition-bound ordered values as the forward boundary.
    /// </summary>
    /// <param name="values">Pre-decoded cursor values bound to the pagination definition.</param>
    /// <returns>A new builder configured for forward pagination.</returns>
    public KeysetQueryBuilder<T> After(PaginationValues<T> values)
        => new(Source, Definition, null, null, values, PaginationDirection.Forward, ShouldIncludeCount, MaxPageSizeValue, SortBy);

    /// <summary>
    /// Sets definition-bound ordered values as the backward boundary.
    /// </summary>
    /// <param name="values">Pre-decoded cursor values bound to the pagination definition.</param>
    /// <returns>A new builder configured for backward pagination.</returns>
    public KeysetQueryBuilder<T> Before(PaginationValues<T> values)
        => new(Source, Definition, null, null, values, PaginationDirection.Backward, ShouldIncludeCount, MaxPageSizeValue, SortBy);

    /// <summary>
    /// Enables total row count computation via a separate SQL query.
    /// </summary>
    /// <returns>A new builder with count enabled.</returns>
    public KeysetQueryBuilder<T> IncludeCount()
        => new(Source, Definition, CursorString, Reference, BoundValues, Direction, true, MaxPageSizeValue, SortBy);

    /// <summary>
    /// Sets the maximum page size. Requests exceeding this value are clamped.
    /// Defaults to 500.
    /// </summary>
    /// <param name="max">The maximum allowed page size. Must be greater than zero.</param>
    /// <returns>A new builder with the page size clamp set.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="max"/> is zero or negative.</exception>
    public KeysetQueryBuilder<T> MaxPageSize(int max)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(max);
        return new(Source, Definition, CursorString, Reference, BoundValues, Direction, ShouldIncludeCount, max, SortBy);
    }

    /// <summary>
    /// Executes the paginated query and returns a page with encoded cursor tokens.
    /// </summary>
    /// <param name="pageSize">The number of items to return.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="CursorPage{T}"/> with items, cursor tokens, and optional total count.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageSize"/> is zero or negative.</exception>
    /// <exception cref="ArgumentException">The cursor string is invalid or expired.</exception>
    /// <exception cref="IncompatibleReferenceException">The reference entity is missing a required property.</exception>
    public Task<CursorPage<T>> TakeAsync(int pageSize, CancellationToken ct = default)
        => Internal.KeysetQueryExecutor.ExecuteAsync(this, pageSize, ct);

    /// <summary>
    /// Streams all remaining pages forward from the current position, advancing automatically.
    /// </summary>
    /// <param name="pageSize">The number of items per batch.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>An async enumerable yielding one page per iteration.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageSize"/> is zero or negative.</exception>
    /// <exception cref="InvalidOperationException"><see cref="Before(string)"/> was called — streaming only supports forward direction.</exception>
    public IAsyncEnumerable<List<T>> StreamAsync(int pageSize, CancellationToken ct = default)
        => Internal.KeysetQueryExecutor.StreamAsync(this, pageSize, ct);

    internal KeysetQueryBuilder<T> WithSortBy(string? sortBy)
        => new(Source, Definition, CursorString, Reference, BoundValues, Direction, ShouldIncludeCount, MaxPageSizeValue, sortBy);
}
