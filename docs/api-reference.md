# API Reference

## Extension Methods on `IQueryable<T>`

### Paginate

```cs
PaginationContext<T> Paginate<T>(
    this IQueryable<T> source,
    PaginationQueryDefinition<T> queryDefinition,
    PaginationDirection direction = Forward,
    object? reference = null)
```

Applies keyset-based ordering and filtering to the query. Returns a context object for further operations.

There is also an overload that accepts an inline builder action instead of a prebuilt definition:

```cs
PaginationContext<T> Paginate<T>(
    this IQueryable<T> source,
    Action<PaginationBuilder<T>> builderAction,
    PaginationDirection direction = Forward,
    object? reference = null)
```

> [!TIP]
> Prefer the `PaginationQueryDefinition<T>` overload for production code. It enables internal expression caching. See [Prebuilt Definitions](prebuilt-definitions.md).

### PaginateQuery

A shortcut that returns the `Query` directly when you don't need the context object:

```cs
IQueryable<T> query = dbContext.Users
    .PaginateQuery(Definition, direction, reference)
    .Take(20);
```

There is also an inline-builder overload:

```cs
IQueryable<T> PaginateQuery<T>(
    this IQueryable<T> source,
    Action<PaginationBuilder<T>> builderAction,
    PaginationDirection direction = Forward,
    object? reference = null)
```

## PaginationContext\<T\>

Returned by `Paginate`. Provides access to the filtered query and navigation helpers.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Query` | `IQueryable<T>` | The filtered and ordered query. Chain `.Take()`, `.Select()`, etc. |
| `OrderedQuery` | `IQueryable<T>` | The ordered query before the cursor filter is applied. Used by `HasPreviousAsync` / `HasNextAsync`. |
| `Direction` | `PaginationDirection` | The direction used for this pagination call. |

### Methods

#### EnsureCorrectOrder

```cs
void EnsureCorrectOrder<TData>(IList<TData> data)
```

Reverses the list in-place when the direction is `Backward`. Always call this after fetching data.

#### HasPreviousAsync / HasNextAsync

```cs
Task<bool> HasPreviousAsync<TData>(IReadOnlyList<TData> data)
Task<bool> HasNextAsync<TData>(IReadOnlyList<TData> data)
```

Checks whether there is more data before/after the current page. The data list can be of any type with matching pagination column properties (see [Loose Typing](loose-typing.md)).

## PaginationQueryDefinition\<T\>

A prebuilt, reusable pagination definition. Created via `PaginationQuery.Build<T>()`.

```cs
static readonly PaginationQueryDefinition<User> Definition =
    PaginationQuery.Build<User>(b => b.Descending(x => x.Created).Ascending(x => x.Id));
```

See [Prebuilt Definitions](prebuilt-definitions.md) for details.

## PaginationQuery

Factory for building reusable definitions.

```cs
PaginationQueryDefinition<T> Build<T>(Action<PaginationBuilder<T>> builderAction)
PaginationQueryDefinition<T> Build<T>(
    string propertyName,
    bool descending,
    string? tiebreaker = "Id",
    bool tiebreakerDescending = false)
```

Use the string overload when sort fields come from request parameters or other runtime configuration.

## PaginationBuilder\<T\>

Fluent builder for defining pagination columns. Used inside `PaginationQuery.Build<T>()` or the inline `Paginate` overload.

### Methods

| Method | Description |
|--------|-------------|
| `Ascending<TColumn>(Expression<Func<T, TColumn>>)` | Adds a column with ascending sort order. |
| `Descending<TColumn>(Expression<Func<T, TColumn>>)` | Adds a column with descending sort order. |

Columns can reference nested properties:

```cs
b.Ascending(x => x.Details.Created)
```

## PaginationDirection

```cs
public enum PaginationDirection
{
    Forward,
    Backward,
}
```

- `Forward` — Walk in the configured sort order (toward "next" pages).
- `Backward` — Walk against the configured sort order (toward "previous" pages).

## PaginationExecutor

Executes a page query and optionally a `COUNT(*)` query.

```cs
Task<KeysetPage<T>> ExecuteAsync<T>(
    IQueryable<T> query,
    PaginationQueryDefinition<T> definition,
    int pageSize,
    object? reference,
    bool includeCount,
    CancellationToken ct = default)
    where T : class
```

Notes:

- `pageSize` must be greater than zero.
- `includeCount: false` returns `TotalCount = -1`.
- The implementation fetches `pageSize + 1` rows internally to compute `HasMore`.

## KeysetPage\<T\>

Returned by `PaginationExecutor.ExecuteAsync`.

| Property | Type | Description |
|----------|------|-------------|
| `Items` | `List<T>` | The current page after trimming the extra probe row. |
| `HasMore` | `bool` | `true` when another page exists after the current page. |
| `TotalCount` | `int` | Total rows in the source query when `includeCount` is `true`; otherwise `-1`. |

## PaginationCursor

Encodes and decodes opaque cursor tokens containing typed keyset values and optional metadata.

```cs
string Encode(ReadOnlySpan<ColumnValue> values, PaginationCursorOptions? options = null)
bool TryDecode(ReadOnlySpan<char> encoded, Span<ColumnValue> values, out int written)
bool TryDecode(
    ReadOnlySpan<char> encoded,
    Span<ColumnValue> values,
    out int written,
    out string? sortBy,
    out int? totalCount)
```

Supported value kinds include:

- `null`, `string`, `bool`, `char`
- integral types, `float`, `double`, `decimal`
- `Guid`, `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, `TimeSpan`
- enum values

`TryDecode` fills the supplied `Span<ColumnValue>` in order and returns `false` for malformed, tampered, or shape-mismatched cursors.

## ColumnValue

`ColumnValue` stores one cursor field:

```cs
public readonly record struct ColumnValue(string Name, object? Value);
```

The `Name` is not serialized into the cursor token. It exists so callers can prepare a destination span with the expected column order and then recover values by semantic name after decode.

## PaginationCursorOptions

Optional metadata stored alongside cursor values:

```cs
public readonly record struct PaginationCursorOptions(
    string? SortBy = null,
    int? TotalCount = null);
```

Use this when the cursor should carry request state such as the selected sort field or the first-page total count.

## PaginationSortRegistry\<T\>

Maps request sort names to prebuilt definitions.

```cs
public sealed class PaginationSortRegistry<T> where T : class
{
    public PaginationSortRegistry(
        PaginationQueryDefinition<T> defaultDefinition,
        params ReadOnlySpan<SortField<T>> fields);

    public PaginationQueryDefinition<T> Resolve(ReadOnlySpan<char> sortBy, ReadOnlySpan<char> sortDir);
}
```

Behavior:

- uses the default definition when `sortBy` is empty or unknown
- selects the descending variant only when `sortDir` equals `"desc"` (case-insensitive)
- uses ascending for all other direction values

## SortField\<T\>

One registry entry containing both direction variants for a logical sort field:

```cs
public readonly record struct SortField<T>(
    string Name,
    PaginationQueryDefinition<T> Ascending,
    PaginationQueryDefinition<T> Descending)
    where T : class;
```

## Nested Properties

Nested properties are supported in pagination definitions. Ensure the reference object has matching nested structure:

```cs
// Loading a reference with Include:
var reference = await dbContext.Users
    .Include(x => x.Details)
    .FirstOrDefaultAsync(x => x.Id == id);

// Or using an anonymous type (loose typing):
var reference = new { Details = new { Created = someDate } };

var context = dbContext.Users.Paginate(
    b => b.Ascending(x => x.Details.Created),
    direction,
    reference);

var users = await context.Query
    .Include(x => x.Details)
    .Take(20)
    .ToListAsync();
```

## Exceptions

### IncompatibleReferenceException

Thrown when a reference object is missing a property required by the pagination definition. Contains:

| Property | Type | Description |
|----------|------|-------------|
| `PropertyName` | `string` | The missing property name. |
| `ReferenceType` | `Type` | The reference object's type. |
| `EntityType` | `Type` | The entity type defining the pagination. |

See [Loose Typing](loose-typing.md) for how to avoid this error.
