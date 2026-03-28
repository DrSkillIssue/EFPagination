# API Reference

## Keyset (Fluent API)

The primary consumer API. Chain `.Keyset()` on any `IQueryable<T>`:

### Entry Points

```cs
// With a prebuilt definition:
KeysetQueryBuilder<T> Keyset<T>(
    this IQueryable<T> source,
    PaginationQueryDefinition<T> definition) where T : class;

// With a sort registry and request (EFPagination.AspNetCore):
KeysetQueryBuilder<T> Keyset<T>(
    this IQueryable<T> source,
    PaginationSortRegistry<T> registry,
    PaginationRequest request) where T : class;
```

### KeysetQueryBuilder\<T\>

A zero-allocation `readonly struct` that accumulates pagination parameters. All methods return a new builder instance.

| Method | Description |
|--------|-------------|
| `After(string cursor)` | Sets the forward cursor. Items after this position are returned. |
| `Before(string cursor)` | Sets the backward cursor. Items before this position are returned. |
| `AfterEntity(object entity)` | Sets an entity as the forward boundary. Properties must match the definition columns. |
| `BeforeEntity(object entity)` | Sets an entity as the backward boundary. |
| `After(PaginationValues<T> values)` | Sets pre-decoded ordered values as the forward boundary. |
| `Before(PaginationValues<T> values)` | Sets pre-decoded ordered values as the backward boundary. |
| `IncludeCount()` | Enables total row count computation via a separate SQL query. |
| `MaxPageSize(int max)` | Sets the maximum page size. Requests exceeding this value are clamped. Defaults to 500. |
| `TakeAsync(int pageSize, CancellationToken ct)` | Executes the query and returns a `CursorPage<T>`. |
| `StreamAsync(int pageSize, CancellationToken ct)` | Streams all remaining pages forward as `IAsyncEnumerable<List<T>>`. |

### CursorPage\<T\>

Returned by `TakeAsync`. Contains the page items and opaque cursor tokens for stateless navigation.

| Property | Type | Description |
|----------|------|-------------|
| `Items` | `List<T>` | The page items in correct order. |
| `NextCursor` | `string?` | Opaque cursor for the next page, or `null` when no more pages. |
| `PreviousCursor` | `string?` | Opaque cursor for the previous page, or `null` on the first page. |
| `TotalCount` | `int` | Total rows when `IncludeCount()` was called; otherwise `-1`. |

### Usage Examples

```cs
// First page
var page = await db.Users.Keyset(definition).TakeAsync(20);

// Forward from cursor
var page = await db.Users.Keyset(definition).After(cursor).TakeAsync(20);

// Backward from cursor
var page = await db.Users.Keyset(definition).Before(cursor).TakeAsync(20);

// With total count and max page size
var page = await db.Users.Keyset(definition).After(cursor).IncludeCount().MaxPageSize(100).TakeAsync(pageSize);

// Entity reference
var page = await db.Users.Keyset(definition).AfterEntity(lastUser).TakeAsync(20);

// Stream all pages
await foreach (var batch in db.Users.Keyset(definition).StreamAsync(100)) { }
```

## PaginationQueryDefinition\<T\>

A prebuilt, reusable pagination definition. Created via `PaginationQuery.Build<T>()`. Stores column metadata and caches expression tree templates for zero per-request overhead.

```cs
static readonly PaginationQueryDefinition<User> Definition =
    PaginationQuery.Build<User>(b => b.Descending(x => x.Created).Ascending(x => x.Id));
```

See [Prebuilt Definitions](prebuilt-definitions.md) for details.

## PaginationQuery

Factory for building reusable definitions.

```cs
// From a builder action (recommended):
PaginationQueryDefinition<T> Build<T>(Action<PaginationBuilder<T>> builderAction);

// From a property name string (for runtime sort fields):
PaginationQueryDefinition<T> Build<T>(
    string propertyName,
    bool descending,
    string? tiebreaker = "Id",
    bool tiebreakerDescending = false);
```

The lambda overload creates a new definition each time -- store the result in a `static readonly` field.

The string overload caches by `(propertyName, descending, tiebreaker, tiebreakerDescending)` per entity type, so repeated calls with the same inputs reuse the same instance. The cache is bounded to 256 entries per type; exceeding it throws `InvalidOperationException`. For user-controlled sort fields, use `PaginationSortRegistry<T>` instead.

## PaginationBuilder\<T\>

Fluent builder for defining pagination columns. Used inside `PaginationQuery.Build<T>()`.

| Method | Description |
|--------|-------------|
| `Ascending<TColumn>(Expression<Func<T, TColumn>>)` | Adds a column with ascending sort order. |
| `Descending<TColumn>(Expression<Func<T, TColumn>>)` | Adds a column with descending sort order. |
| `ConfigureColumn<TColumn>(Expression<Func<T, TColumn>>, bool isDescending)` | Adds a column with explicit sort direction. |

Columns can reference nested properties:

```cs
b.Ascending(x => x.Details.Created)
```

## PaginationSortRegistry\<T\>

Maps request sort names to prebuilt definitions.

```cs
public sealed class PaginationSortRegistry<T>
{
    public PaginationSortRegistry(
        PaginationQueryDefinition<T> defaultDefinition,
        params ReadOnlySpan<SortField<T>> fields);

    public PaginationQueryDefinition<T> Resolve(
        ReadOnlySpan<char> sortBy,
        ReadOnlySpan<char> sortDir);

    public bool TryResolve(
        ReadOnlySpan<char> sortBy,
        ReadOnlySpan<char> sortDir,
        out PaginationQueryDefinition<T> definition);
}
```

Behavior:

- `Resolve` uses the default definition when `sortBy` is empty or unknown.
- `TryResolve` returns `false` when `sortBy` does not match any registered field (empty `sortBy` returns the default and `true`).
- Selects the descending variant only when `sortDir` equals `"desc"` (case-insensitive).
- Uses ascending for all other direction values.

## SortField\<T\> / SortField

One registry entry containing both direction variants for a logical sort field:

```cs
public readonly record struct SortField<T>(
    string Name,
    PaginationQueryDefinition<T> Ascending,
    PaginationQueryDefinition<T> Descending);
```

Use the `SortField.Create<T>` factory to build both definitions from a property name:

```cs
public static SortField<T> Create<T>(
    string name,
    string propertyName,
    string? tiebreaker = "Id",
    bool tiebreakerDescending = false);
```

```cs
SortField.Create<User>("created", "Created")
// Equivalent to manually constructing:
// new SortField<User>(
//     "created",
//     PaginationQuery.Build<User>("Created", descending: false, tiebreaker: "Id"),
//     PaginationQuery.Build<User>("Created", descending: true, tiebreaker: "Id"))
```

## PaginationCursor

Encodes and decodes opaque cursor tokens containing typed keyset values and optional metadata.

### Encode

```cs
string Encode(ReadOnlySpan<ColumnValue> values, PaginationCursorOptions? options = null);
```

### TryDecode

```cs
// Basic decode into a ColumnValue buffer:
bool TryDecode(
    ReadOnlySpan<char> encoded,
    Span<ColumnValue> values,
    out int written,
    byte[]? signingKey = null);

// Decode with metadata extraction:
bool TryDecode(
    ReadOnlySpan<char> encoded,
    Span<ColumnValue> values,
    out int written,
    out string? sortBy,
    out int? totalCount,
    byte[]? signingKey = null);

// Definition-based decode into PaginationValues<T>:
bool TryDecode<T>(
    ReadOnlySpan<char> encoded,
    PaginationQueryDefinition<T> definition,
    out PaginationValues<T> values,
    out int written);

// Definition-based decode with metadata:
bool TryDecode<T>(
    ReadOnlySpan<char> encoded,
    PaginationQueryDefinition<T> definition,
    out PaginationValues<T> values,
    out int written,
    out string? sortBy,
    out int? totalCount);
```

When a `signingKey` is passed, `TryDecode` verifies the HMAC-SHA256 signature appended by `Encode` and returns `false` if verification fails.

The definition-based overloads additionally verify the schema fingerprint embedded in the cursor, rejecting cursors that were encoded against a different definition shape.

Supported value kinds include:

- `null`, `string`, `bool`, `char`
- integral types, `float`, `double`, `decimal`
- `Guid`, `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, `TimeSpan`
- enum values

`TryDecode` fills the supplied buffer in order and returns `false` for malformed, tampered, or shape-mismatched cursors.

## ColumnValue

Stores one cursor field:

```cs
public readonly record struct ColumnValue(string Name, object? Value);
```

The `Name` is not serialized into the cursor token. It exists so callers can prepare a destination span with the expected column order and then recover values by semantic name after decode.

## PaginationCursorOptions

Optional metadata stored alongside cursor values:

```cs
public readonly record struct PaginationCursorOptions(
    string? SortBy = null,
    int? TotalCount = null,
    uint? SchemaFingerprint = null,
    byte[]? SigningKey = null);
```

| Parameter | Description |
|-----------|-------------|
| `SortBy` | Logical sort key to embed in the cursor (round-tripped through decode). |
| `TotalCount` | Total row count to carry forward so subsequent pages skip the `COUNT` query. |
| `SchemaFingerprint` | Definition fingerprint for detecting stale cursors from schema changes. |
| `SigningKey` | HMAC-SHA256 key. When set, `Encode` appends a truncated HMAC; `TryDecode` verifies it. |

## PaginationValues\<T\>

Ordered pagination boundary values bound to a specific `PaginationQueryDefinition<T>`.

```cs
public static PaginationValues<T> Create(params object?[] values);
```

Create a `PaginationValues<T>` manually when you have the values in definition column order:

```cs
var values = PaginationValues<User>.Create(lastCreated, lastId);
var page = await dbContext.Users
    .Keyset(definition)
    .After(values)
    .TakeAsync(20);
```

The definition-based `PaginationCursor.TryDecode<T>` overload also returns a `PaginationValues<T>`.

## PaginationDirection

```cs
public enum PaginationDirection
{
    Forward,
    Backward,
};
```

- `Forward` -- Walk in the configured sort order (toward "next" pages).
- `Backward` -- Walk against the configured sort order (toward "previous" pages).

## KeysetPage\<T\>

Returned by the low-level `MaterializeAsync` and `PaginationExecutor.ExecuteAsync`.

| Property | Type | Description |
|----------|------|-------------|
| `Items` | `List<T>` | The current page items in correct order. |
| `HasPrevious` | `bool` | `true` when a previous page exists. |
| `HasNext` | `bool` | `true` when a next page exists. |
| `TotalCount` | `int` | Total rows when requested; otherwise `-1`. |

## PaginationStreaming

Provides `IAsyncEnumerable<List<T>>`-based streaming pagination that automatically advances through all pages. This is the low-level static method; prefer `builder.StreamAsync()` for most uses.

```cs
public static async IAsyncEnumerable<List<T>> PaginateAllAsync<T>(
    IQueryable<T> query,
    PaginationQueryDefinition<T> definition,
    int pageSize,
    CancellationToken ct = default) where T : class;
```

## Nested Properties

Nested properties are supported in pagination definitions:

```cs
PaginationQuery.Build<User>(b => b.Ascending(x => x.Details.Created).Ascending(x => x.Id));
```

When using the low-level `Paginate` API with nested properties, ensure the reference object has matching nested structure:

```cs
var reference = await dbContext.Users
    .Include(x => x.Details)
    .FirstOrDefaultAsync(x => x.Id == id);

var context = dbContext.Users.Paginate(definition, direction, reference);
```

Or use an anonymous type:

```cs
var reference = new { Details = new { Created = someDate }, Id = someId };
```

## Low-Level Paginate API

For consumers who need raw `IQueryable` composition (e.g., `.Include()` between pagination and materialization), the `Paginate` extension methods provide direct access to the filtered query.

### Paginate

```cs
PaginationContext<T> Paginate<T>(
    this IQueryable<T> source,
    PaginationQueryDefinition<T> queryDefinition,
    PaginationDirection direction = PaginationDirection.Forward,
    object? reference = null)
```

Additional overloads accept `PaginationValues<T>`, a strongly-typed `TReference`, `ReadOnlySpan<ColumnValue>`, or `ColumnValue[]` as the reference.

> [!TIP]
> The fluent `Keyset()` API handles cursor decoding, order correction, and cursor encoding automatically. Use `Paginate` only when you need to compose additional `IQueryable` operators (such as `.Include()`) between pagination and materialization.

### PaginationContext\<T\>

Returned by `Paginate`. Provides access to the filtered query and navigation helpers.

| Property | Type | Description |
|----------|------|-------------|
| `Query` | `IQueryable<T>` | The filtered and ordered query. Chain `.Take()`, `.Include()`, `.Select()`, etc. |
| `OrderedQuery` | `IQueryable<T>` | The ordered query before the cursor filter is applied. Used by `HasPreviousAsync` / `HasNextAsync`. |
| `Direction` | `PaginationDirection` | The direction used for this pagination call. |

### MaterializeAsync

Materializes a paginated query, computing `HasPrevious` and `HasNext` without extra SQL roundtrips by leveraging the `pageSize + 1` overflow pattern and direction-aware inference.

```cs
Task<KeysetPage<T>> MaterializeAsync<T>(
    this PaginationContext<T> context,
    int pageSize,
    CancellationToken ct = default)
```

```cs
var page = await dbContext.Users
    .Paginate(definition, PaginationDirection.Forward, reference)
    .MaterializeAsync(20);
```

### EnsureCorrectOrder

```cs
void EnsureCorrectOrder<TData>(this PaginationContext<T> context, IList<TData> data)
```

Reverses the list in-place when the direction is `Backward`. Call this after manual fetching via `context.Query.Take(n).ToListAsync()`.

### ToCorrectOrder

```cs
IReadOnlyList<T2> ToCorrectOrder<T, T2>(this PaginationContext<T> context, IReadOnlyList<T2> data)
```

Returns a read-only view of items in correct order. Zero allocation for forward direction; reverse-indexed wrapper for backward.

### HasPreviousAsync / HasNextAsync

```cs
Task<bool> HasPreviousAsync<TData>(this PaginationContext<T> context, IReadOnlyList<TData> data)
Task<bool> HasNextAsync<TData>(this PaginationContext<T> context, IReadOnlyList<TData> data)
```

Checks whether there is more data before/after the current page. Each check executes an `AnyAsync` query (one roundtrip per call). The data list can be of any type with matching pagination column properties (see [Loose Typing](loose-typing.md)).

For zero-roundtrip boundary checks, prefer `MaterializeAsync` instead.

### Low-Level Example: Include Composition

```cs
var context = dbContext.Users.Paginate(definition, direction, reference);

var users = await context.Query
    .Include(x => x.Details)
    .Take(20)
    .ToListAsync();

context.EnsureCorrectOrder(users);
var hasPrevious = await context.HasPreviousAsync(users);
var hasNext = await context.HasNextAsync(users);
```

## PaginationExecutor

Static helper that combines `Paginate`, materialization, and count into a single call. Returns `KeysetPage<T>` or `CursorPage<T>`.

This is an advanced API. Prefer the fluent `Keyset()` builder for most uses.

### ExecuteAsync

```cs
Task<KeysetPage<T>> ExecuteAsync<T>(
    IQueryable<T> query,
    PaginationQueryDefinition<T> definition,
    ExecutionOptions options,
    object? reference = null,
    CancellationToken ct = default) where T : class;

Task<KeysetPage<T>> ExecuteAsync<T>(
    IQueryable<T> query,
    PaginationQueryDefinition<T> definition,
    ExecutionOptions options,
    PaginationValues<T> referenceValues,
    CancellationToken ct = default) where T : class;

Task<KeysetPage<T>> ExecuteAsync<T>(
    IQueryable<T> query,
    PaginationQueryDefinition<T> definition,
    ExecutionOptions options,
    ReadOnlySpan<ColumnValue> referenceValues,
    CancellationToken ct = default) where T : class;
```

### ExecutionOptions

Controls `PaginationExecutor` materialization:

```cs
public readonly record struct ExecutionOptions(
    int PageSize,
    PaginationDirection Direction = PaginationDirection.Forward,
    bool IncludeCount = false,
    int MaxPageSize = 500);
```

## EFPagination.AspNetCore

### PaginationRequest

Binds cursor-based pagination parameters from the query string:

```cs
public readonly record struct PaginationRequest(
    string? After = null,
    string? Before = null,
    int PageSize = 25,
    string? SortBy = null,
    string? SortDir = null);
```

When `Before` is provided, the query paginates backward from that cursor. When both `After` and `Before` are provided, `Before` takes precedence.

### FromRequest

Applies cursor and direction from a `PaginationRequest` to an existing builder:

```cs
KeysetQueryBuilder<T> FromRequest<T>(
    this KeysetQueryBuilder<T> builder,
    PaginationRequest request) where T : class;
```

### Keyset (Registry + Request)

Creates a builder from a sort registry and pagination request, resolving the definition from the request's sort parameters and applying cursors:

```cs
KeysetQueryBuilder<T> Keyset<T>(
    this IQueryable<T> source,
    PaginationSortRegistry<T> registry,
    PaginationRequest request) where T : class;
```

### PaginatedResponse\<T\>

JSON-serializable paginated response envelope:

```cs
public sealed record PaginatedResponse<T>(
    IReadOnlyList<T> Items,
    string? NextCursor,
    string? PreviousCursor,
    int? TotalCount);
```

### ToPaginatedResponse

Converts a `CursorPage<T>` to a `PaginatedResponse<T>`:

```cs
PaginatedResponse<T> ToPaginatedResponse<T>(this CursorPage<T> page);

PaginatedResponse<TOut> ToPaginatedResponse<T, TOut>(
    this CursorPage<T> page,
    Func<T, TOut> selector);
```

The `TotalCount` is mapped to `null` when the source value is negative.

## Exceptions

### IncompatibleReferenceException

Thrown when a reference object is missing a property required by the pagination definition. Contains:

| Property | Type | Description |
|----------|------|-------------|
| `PropertyName` | `string` | The missing property name. |
| `ReferenceType` | `Type` | The reference object's type. |
| `EntityType` | `Type` | The entity type defining the pagination. |

See [Loose Typing](loose-typing.md) for how to avoid this error.
