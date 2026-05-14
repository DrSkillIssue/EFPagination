namespace EFPagination;

/// <summary>
/// A zero-allocation fluent builder for keyset pagination. Accumulates pagination parameters
/// and executes via <see cref="TakeAsync"/> or <see cref="StreamAsync"/>.
/// </summary>
/// <typeparam name="T">The entity type being paginated.</typeparam>
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

    /// <summary>Sets the forward cursor. Items after this cursor position are returned.</summary>
    public KeysetQueryBuilder<T> After(string cursor)
        => this with { CursorString = cursor, Reference = null, BoundValues = default, Direction = PaginationDirection.Forward };

    /// <summary>Sets the backward cursor. Items before this cursor position are returned.</summary>
    public KeysetQueryBuilder<T> Before(string cursor)
        => this with { CursorString = cursor, Reference = null, BoundValues = default, Direction = PaginationDirection.Backward };

    /// <summary>Sets an entity as the forward boundary.</summary>
    public KeysetQueryBuilder<T> AfterEntity(object entity)
        => this with { CursorString = null, Reference = entity, BoundValues = default, Direction = PaginationDirection.Forward };

    /// <summary>Sets an entity as the backward boundary.</summary>
    public KeysetQueryBuilder<T> BeforeEntity(object entity)
        => this with { CursorString = null, Reference = entity, BoundValues = default, Direction = PaginationDirection.Backward };

    /// <summary>Sets definition-bound ordered values as the forward boundary.</summary>
    public KeysetQueryBuilder<T> After(PaginationValues<T> values)
        => this with { CursorString = null, Reference = null, BoundValues = values, Direction = PaginationDirection.Forward };

    /// <summary>Sets definition-bound ordered values as the backward boundary.</summary>
    public KeysetQueryBuilder<T> Before(PaginationValues<T> values)
        => this with { CursorString = null, Reference = null, BoundValues = values, Direction = PaginationDirection.Backward };

    /// <summary>Enables total row count computation via a separate SQL query.</summary>
    public KeysetQueryBuilder<T> IncludeCount() => this with { ShouldIncludeCount = true };

    /// <summary>
    /// Sets the maximum page size. Requests exceeding this value are clamped. Defaults to 500.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="max"/> is zero or negative.</exception>
    public KeysetQueryBuilder<T> MaxPageSize(int max)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(max);
        return this with { MaxPageSizeValue = max };
    }

    /// <summary>Executes the paginated query and returns a page with encoded cursor tokens.</summary>
    public Task<CursorPage<T>> TakeAsync(int pageSize, CancellationToken ct = default)
        => Internal.KeysetQueryExecutor.ExecuteAsync(this, pageSize, ct);

    /// <summary>Streams all remaining pages forward, advancing automatically.</summary>
    public IAsyncEnumerable<List<T>> StreamAsync(int pageSize, CancellationToken ct = default)
        => Internal.KeysetQueryExecutor.StreamAsync(this, pageSize, ct);

    internal KeysetQueryBuilder<T> WithSortBy(string? sortBy) => this with { SortBy = sortBy };
}
