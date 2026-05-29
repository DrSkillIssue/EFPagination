using System.Linq.Expressions;

namespace EFPagination;

/// <summary>
/// A zero-allocation fluent builder for keyset (seek-style) pagination over an
/// <see cref="IQueryable{T}"/> source. Accumulates pagination parameters across
/// <see langword="with"/>-expression returns and executes via
/// <see cref="TakeAsync(int, CancellationToken)"/> or
/// <see cref="StreamAsync(int, CancellationToken)"/>.
/// </summary>
/// <typeparam name="T">The entity type being paginated.</typeparam>
/// <remarks>
/// Obtain an instance by calling
/// <see cref="KeysetPaginationExtensions.Keyset{T}(IQueryable{T}, PaginationQueryDefinition{T})"/>
/// on a query. Each chained call returns a new builder; the original instance is unchanged.
/// </remarks>
public readonly record struct KeysetQueryBuilder<T> where T : class
{
    private const int DefaultMaxPageSize = 500;

    internal IQueryable<T> Source { get; init; }
    internal PaginationQueryDefinition<T> Definition { get; init; }
    internal string? CursorString { get; init; }
    internal object? Reference { get; init; }
    internal PaginationValues<T> BoundValues { get; init; }
    internal PaginationDirection Direction { get; init; }
    internal bool ShouldIncludeCount { get; init; }
    internal int MaxPageSizeValue { get; init; }
    internal string? SortBy { get; init; }

    internal KeysetQueryBuilder(IQueryable<T> source, PaginationQueryDefinition<T> definition)
    {
        Source = source;
        Definition = definition;
        MaxPageSizeValue = DefaultMaxPageSize;
    }

    /// <summary>
    /// Sets the forward cursor. Items after this cursor position are returned in ascending
    /// order of the pagination definition.
    /// </summary>
    /// <param name="cursor">
    /// An opaque cursor string previously produced by <see cref="CursorPage{T}.NextCursor"/>.
    /// </param>
    /// <returns>A new builder configured for forward pagination from <paramref name="cursor"/>.</returns>
    public KeysetQueryBuilder<T> After(string cursor)
        => this with { CursorString = cursor, Reference = null, BoundValues = default, Direction = PaginationDirection.Forward };

    /// <summary>
    /// Sets the backward cursor. Items before this cursor position are returned in the
    /// pagination definition's natural order.
    /// </summary>
    /// <param name="cursor">
    /// An opaque cursor string previously produced by <see cref="CursorPage{T}.PreviousCursor"/>.
    /// </param>
    /// <returns>A new builder configured for backward pagination from <paramref name="cursor"/>.</returns>
    public KeysetQueryBuilder<T> Before(string cursor)
        => this with { CursorString = cursor, Reference = null, BoundValues = default, Direction = PaginationDirection.Backward };

    /// <summary>
    /// Sets an entity as the forward boundary. Items after this entity are returned. The
    /// entity must have properties matching every column in the pagination definition.
    /// </summary>
    /// <param name="entity">The reference entity whose property values define the cursor position.</param>
    /// <returns>A new builder configured for forward pagination from <paramref name="entity"/>.</returns>
    public KeysetQueryBuilder<T> AfterEntity(object entity)
        => this with { CursorString = null, Reference = entity, BoundValues = default, Direction = PaginationDirection.Forward };

    /// <summary>
    /// Sets an entity as the backward boundary. Items before this entity are returned. The
    /// entity must have properties matching every column in the pagination definition.
    /// </summary>
    /// <param name="entity">The reference entity whose property values define the cursor position.</param>
    /// <returns>A new builder configured for backward pagination from <paramref name="entity"/>.</returns>
    public KeysetQueryBuilder<T> BeforeEntity(object entity)
        => this with { CursorString = null, Reference = entity, BoundValues = default, Direction = PaginationDirection.Backward };

    /// <summary>
    /// Sets definition-bound ordered values as the forward boundary.
    /// </summary>
    /// <param name="values">Pre-decoded cursor values bound to the pagination definition.</param>
    /// <returns>A new builder configured for forward pagination from <paramref name="values"/>.</returns>
    public KeysetQueryBuilder<T> After(PaginationValues<T> values)
        => this with { CursorString = null, Reference = null, BoundValues = values, Direction = PaginationDirection.Forward };

    /// <summary>
    /// Sets definition-bound ordered values as the backward boundary.
    /// </summary>
    /// <param name="values">Pre-decoded cursor values bound to the pagination definition.</param>
    /// <returns>A new builder configured for backward pagination from <paramref name="values"/>.</returns>
    public KeysetQueryBuilder<T> Before(PaginationValues<T> values)
        => this with { CursorString = null, Reference = null, BoundValues = values, Direction = PaginationDirection.Backward };

    /// <summary>
    /// Enables the total row count on <see cref="CursorPage{T}.TotalCount"/>, computed once on a
    /// cursor-less request and carried forward through later cursors.
    /// </summary>
    /// <returns>A new builder with count computation enabled.</returns>
    public KeysetQueryBuilder<T> IncludeCount() => this with { ShouldIncludeCount = true };

    /// <summary>
    /// Sets the maximum page size. Requests exceeding this value are clamped to <paramref name="max"/>.
    /// Defaults to <c>500</c>.
    /// </summary>
    /// <param name="max">The maximum allowed page size. Must be greater than zero.</param>
    /// <returns>A new builder with the page-size clamp set.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="max"/> is zero or negative.</exception>
    public KeysetQueryBuilder<T> MaxPageSize(int max)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(max);
        return this with { MaxPageSizeValue = max };
    }

    /// <summary>
    /// Executes the paginated query and returns a page with encoded cursor tokens.
    /// </summary>
    /// <param name="pageSize">
    /// The number of items to return per page (clamped by <see cref="MaxPageSize(int)"/>).
    /// </param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="CursorPage{T}"/> with items, cursor tokens, and optional total count.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageSize"/> is zero or negative.</exception>
    /// <exception cref="ArgumentException">The cursor string is invalid or expired.</exception>
    /// <exception cref="IncompatibleReferenceException">The reference entity is missing a required property.</exception>
    public Task<CursorPage<T>> TakeAsync(int pageSize, CancellationToken ct = default)
        => Internal.KeysetQueryExecutor.ExecuteAsync(this, pageSize, ct);

    /// <summary>
    /// Streams all remaining pages forward from the current position, advancing automatically.
    /// Each iteration materialises one batch as a <see cref="List{T}"/>.
    /// </summary>
    /// <param name="pageSize">The number of items per batch.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>An async enumerable that yields one page per iteration until the source is exhausted.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageSize"/> is zero or negative.</exception>
    /// <exception cref="InvalidOperationException">
    /// <see cref="Before(string)"/> or <see cref="Before(PaginationValues{T})"/> was applied — streaming only supports forward direction.
    /// </exception>
    public IAsyncEnumerable<List<T>> StreamAsync(int pageSize, CancellationToken ct = default)
        => Internal.KeysetQueryExecutor.StreamAsync(this, pageSize, ct);

    /// <summary>
    /// Executes the paginated query with a server-side projection and returns a page of the
    /// projected type with encoded cursor tokens. The projection runs inside the SQL statement
    /// that applies the keyset ORDER BY, so subqueries inside <paramref name="selector"/> stay
    /// server-side and the <c>SELECT</c> list materialises only the projected columns.
    /// </summary>
    /// <typeparam name="TOut">The projected DTO type.</typeparam>
    /// <param name="pageSize">
    /// The number of items to return per page (clamped by <see cref="MaxPageSize(int)"/>).
    /// </param>
    /// <param name="selector">
    /// A server-translatable projection from <typeparamref name="T"/> to <typeparamref name="TOut"/>.
    /// </param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="CursorPage{TOut}"/> with projected items, cursor tokens, and optional total count.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="selector"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageSize"/> is zero or negative.</exception>
    /// <exception cref="ArgumentException">The cursor string is invalid or expired.</exception>
    /// <exception cref="NotSupportedException">
    /// The pagination definition has fewer than 1 or more than 8 key columns.
    /// </exception>
    public Task<CursorPage<TOut>> TakeAsync<TOut>(int pageSize, Expression<Func<T, TOut>> selector, CancellationToken ct = default)
        => Internal.KeysetProjectionExecutor.ExecuteAsync(this, selector, pageSize, ct);

    /// <summary>
    /// Streams all remaining pages forward with a server-side projection, advancing automatically.
    /// Each iteration materialises one batch as a <see cref="List{TOut}"/>.
    /// </summary>
    /// <typeparam name="TOut">The projected DTO type.</typeparam>
    /// <param name="pageSize">The number of items per batch.</param>
    /// <param name="selector">
    /// A server-translatable projection from <typeparamref name="T"/> to <typeparamref name="TOut"/>.
    /// </param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>An async enumerable that yields one projected page per iteration until the source is exhausted.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="selector"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageSize"/> is zero or negative.</exception>
    /// <exception cref="InvalidOperationException">
    /// <see cref="Before(string)"/> or <see cref="Before(PaginationValues{T})"/> was applied — streaming only supports forward direction.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// The pagination definition has fewer than 1 or more than 8 key columns.
    /// </exception>
    public IAsyncEnumerable<List<TOut>> StreamAsync<TOut>(int pageSize, Expression<Func<T, TOut>> selector, CancellationToken ct = default)
        => Internal.KeysetProjectionExecutor.StreamAsync(this, selector, pageSize, ct);

    internal KeysetQueryBuilder<T> WithSortBy(string? sortBy) => this with { SortBy = sortBy };
}
