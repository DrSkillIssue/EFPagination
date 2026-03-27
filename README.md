# Entity Framework Pagination (EFPagination)

Keyset pagination for Entity Framework Core. Also known as seek or cursor pagination.

Keyset pagination delivers **stable query performance regardless of page depth**, unlike offset pagination (`Skip`/`Take`) which degrades linearly as you skip more rows.

## Installation

```
dotnet add package EFPagination
```

Requires .NET 10 and EF Core 10.

## Quick Start

```cs
using EFPagination;

// Quick-start local variable; in production, build once and store as a static readonly field.
var definition = PaginationQuery.Build<User>(b => b.Descending(x => x.Created).Ascending(x => x.Id));

// First page.
var context = dbContext.Users.Paginate(definition);
var users = await context.Query.Take(20).ToListAsync();
context.EnsureCorrectOrder(users);

// Next page — pass the last item as the reference.
var nextContext = dbContext.Users.Paginate(definition, PaginationDirection.Forward, users[^1]);
var nextUsers = await nextContext.Query.Take(20).ToListAsync();
nextContext.EnsureCorrectOrder(nextUsers);

// Check boundaries.
var hasPrevious = await nextContext.HasPreviousAsync(nextUsers);
var hasNext = await nextContext.HasNextAsync(nextUsers);
```

## What's Included

- Prebuilt and inline keyset pagination over `IQueryable<T>`
- Strongly-typed reference overloads to avoid loose-typing overhead
- Runtime sort definitions via `PaginationQuery.Build<T>(string, ...)`
- Opaque cursor token encoding/decoding with `PaginationCursor`
- Direct `ColumnValue` pagination APIs for member-access definitions
- Page execution with optional total-count retrieval via `PaginationExecutor`, including direct `ColumnValue` input
- Sort field registries for request-driven sorting via `PaginationSortRegistry<T>`
- Roslyn analyzer support for nullable pagination columns

## API

### Paginate

Applies keyset-based ordering and filtering to a query.

```cs
// With a prebuilt definition (recommended — enables expression caching):
PaginationContext<T> Paginate<T>(
    this IQueryable<T> source,
    PaginationQueryDefinition<T> queryDefinition,
    PaginationDirection direction = PaginationDirection.Forward,
    object? reference = null);

// With an inline builder:
PaginationContext<T> Paginate<T>(
    this IQueryable<T> source,
    Action<PaginationBuilder<T>> builderAction,
    PaginationDirection direction = PaginationDirection.Forward,
    object? reference = null)

// With a strongly-typed reference:
PaginationContext<T> Paginate<T, TReference>(
    this IQueryable<T> source,
    PaginationQueryDefinition<T> queryDefinition,
    PaginationDirection direction,
    TReference reference)

// With definition-bound ordered values:
PaginationContext<T> Paginate<T>(
    this IQueryable<T> source,
    PaginationQueryDefinition<T> queryDefinition,
    PaginationDirection direction,
    PaginationValues<T> referenceValues)

// With direct column values:
PaginationContext<T> Paginate<T>(
    this IQueryable<T> source,
    PaginationQueryDefinition<T> queryDefinition,
    PaginationDirection direction,
    ReadOnlySpan<ColumnValue> referenceValues)
```

### PaginateQuery

Shortcut that returns the `IQueryable<T>` directly when you don't need the context:

```cs
// With a prebuilt definition (recommended):
IQueryable<T> PaginateQuery<T>(
    this IQueryable<T> source,
    PaginationQueryDefinition<T> queryDefinition,
    PaginationDirection direction = PaginationDirection.Forward,
    object? reference = null);

// With an inline builder:
IQueryable<T> PaginateQuery<T>(
    this IQueryable<T> source,
    Action<PaginationBuilder<T>> builderAction,
    PaginationDirection direction = PaginationDirection.Forward,
    object? reference = null)

// With a strongly-typed reference:
IQueryable<T> PaginateQuery<T, TReference>(
    this IQueryable<T> source,
    PaginationQueryDefinition<T> queryDefinition,
    PaginationDirection direction,
    TReference reference)

// With definition-bound ordered values:
IQueryable<T> PaginateQuery<T>(
    this IQueryable<T> source,
    PaginationQueryDefinition<T> queryDefinition,
    PaginationDirection direction,
    PaginationValues<T> referenceValues)

// With direct column values:
IQueryable<T> PaginateQuery<T>(
    this IQueryable<T> source,
    PaginationQueryDefinition<T> queryDefinition,
    PaginationDirection direction,
    ReadOnlySpan<ColumnValue> referenceValues)
```

```cs
var query = dbContext.Users
    .PaginateQuery(definition, direction, reference)
    .Take(20);
```

### PaginationContext\<T\>

Returned by `Paginate`.

| Property | Description |
|----------|-------------|
| `Query` | The filtered and ordered `IQueryable<T>`. Chain `.Take()`, `.Select()`, etc. |
| `OrderedQuery` | The ordered `IQueryable<T>` without the pagination filter. Used internally by boundary checks. |
| `Direction` | The `PaginationDirection` used for this call. |

Extension methods on `PaginationContext<T>`:

| Method | Description |
|--------|-------------|
| `EnsureCorrectOrder<T2>(IList<T2>)` | Reverses the list in-place when direction is `Backward`. Always call after fetching. |
| `HasPreviousAsync<T2>(IReadOnlyList<T2>)` | Returns `Task<bool>` — `true` if there is data before the current page. |
| `HasNextAsync<T2>(IReadOnlyList<T2>)` | Returns `Task<bool>` — `true` if there is data after the current page. |

### PaginationQueryDefinition\<T\>

A prebuilt, reusable pagination definition. Build once, reuse across requests:

```cs
static readonly PaginationQueryDefinition<User> Definition =
    PaginationQuery.Build<User>(b => b.Descending(x => x.Created).Ascending(x => x.Id));
```

### PaginationQuery

Factory for building reusable definitions:

```cs
PaginationQueryDefinition<T> Build<T>(Action<PaginationBuilder<T>> builderAction)

PaginationQueryDefinition<T> Build<T>(
    string propertyName,
    bool descending,
    string? tiebreaker = "Id",
    bool tiebreakerDescending = false)
```

Use the string overload when sort fields come from request parameters or other runtime configuration.

### PaginationBuilder\<T\>

Fluent builder for defining pagination columns, used inside `PaginationQuery.Build<T>()`:

| Method | Description |
|--------|-------------|
| `Ascending<TCol>(Expression<Func<T, TCol>>)` | Adds a column with ascending sort. |
| `Descending<TCol>(Expression<Func<T, TCol>>)` | Adds a column with descending sort. |
| `ConfigureColumn<TCol>(Expression<Func<T, TCol>>, bool isDescending)` | Adds a column with explicit sort direction. Useful when the direction is dynamic. |

Nested properties are supported: `b.Ascending(x => x.Details.Created)`.

### PaginationExecutor / KeysetPage\<T\>

Execute a page query and optionally fetch the total count:

```cs
Task<KeysetPage<T>> ExecuteAsync<T>(
    IQueryable<T> query,
    PaginationQueryDefinition<T> definition,
    int pageSize,
    object? reference,
    bool includeCount,
    CancellationToken ct = default)
    where T : class

Task<KeysetPage<T>> ExecuteAsync<T>(
    IQueryable<T> query,
    PaginationQueryDefinition<T> definition,
    int pageSize,
    bool includeCount,
    PaginationValues<T> referenceValues,
    CancellationToken ct = default)
    where T : class

Task<KeysetPage<T>> ExecuteAsync<T>(
    IQueryable<T> query,
    PaginationQueryDefinition<T> definition,
    int pageSize,
    bool includeCount,
    ReadOnlySpan<ColumnValue> referenceValues,
    CancellationToken ct = default)
    where T : class
```

Use the `PaginationValues<T>` overload as the canonical high-performance path for definition-based cursor decoding. Use the `ColumnValue` overload only when you already have manual member-addressable name/value pairs.

`KeysetPage<T>` contains:

| Property | Description |
|----------|-------------|
| `Items` | The page items. |
| `HasMore` | `true` when another page exists after the current page. |
| `TotalCount` | Total rows when `includeCount` is `true`; otherwise `-1`. |

### PaginationCursor / ColumnValue / PaginationCursorOptions

Encode and decode opaque cursor tokens that preserve typed keyset values and optional metadata:

```cs
string Encode(ReadOnlySpan<ColumnValue> values, PaginationCursorOptions? options = null)
bool TryDecode(ReadOnlySpan<char> encoded, Span<ColumnValue> values, out int written)
bool TryDecode(
    ReadOnlySpan<char> encoded,
    Span<ColumnValue> values,
    out int written,
    out string? sortBy,
    out int? totalCount)
bool TryDecode<T>(
    ReadOnlySpan<char> encoded,
    PaginationQueryDefinition<T> definition,
    out PaginationValues<T> values,
    out int written)
bool TryDecode<T>(
    ReadOnlySpan<char> encoded,
    PaginationQueryDefinition<T> definition,
    out PaginationValues<T> values,
    out int written,
    out string? sortBy,
    out int? totalCount)
```

Supported cursor value types include strings, booleans, numeric primitives, `Guid`, `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, `TimeSpan`, and enums.

`ColumnValue`-based pagination works for member-access definitions such as `x => x.Created` or `x => x.Id`. Computed expressions such as `x => x.CreatedNullable ?? DateTime.MinValue` remain supported through regular reference-object overloads, but they are not addressable by `ColumnValue.Name`.

Use the definition-based `TryDecode` overload when you want a ready-to-use `PaginationValues<T>` container for `PaginationExecutor`, `Paginate`, or `PaginateQuery` without manually pre-populating column names.

Example:

```cs
var page = await PaginationExecutor.ExecuteAsync(dbContext.Users, definition, 20, reference: null, includeCount: true);

var last = page.Items[^1];
var nextCursor = PaginationCursor.Encode(
[
    new ColumnValue(nameof(User.Created), last.Created),
    new ColumnValue(nameof(User.Id), last.Id),
],
new PaginationCursorOptions(TotalCount: page.TotalCount));

if (PaginationCursor.TryDecode(nextCursor, definition, out var values, out _))
{
    var nextPage = await PaginationExecutor.ExecuteAsync(
        dbContext.Users,
        definition,
        20,
        includeCount: false,
        values);
}
```

### PaginationSortRegistry\<T\> / SortField\<T\>

Map request sort names to prebuilt definitions:

```cs
var sorts = new PaginationSortRegistry<User>(
    defaultDefinition: PaginationQuery.Build<User>(b => b.Descending(x => x.Created).Ascending(x => x.Id)),
    new SortField<User>(
        "created",
        PaginationQuery.Build<User>("Created", descending: false, tiebreaker: "Id"),
        PaginationQuery.Build<User>("Created", descending: true, tiebreaker: "Id")));

var definition = sorts.Resolve(sortBy, sortDir);
```

`Resolve` is case-insensitive and falls back to the default definition when the requested sort field is empty or unknown.

### IncompatibleReferenceException

Thrown when a reference object is missing a property required by the pagination definition (loose typing mismatch).

| Property | Description |
|----------|-------------|
| `PropertyName` | The property that was not found on the reference object. |
| `ReferenceType` | The type of the reference object that was searched. |
| `EntityType` | The entity type that defines the pagination column. |

### PaginationDirection

```cs
enum PaginationDirection { Forward, Backward }
```

- `Forward` — walk in the configured sort order (toward next pages).
- `Backward` — walk against the configured sort order (toward previous pages).

## Documentation

| Topic | Description |
|-------|-------------|
| [Getting Started](docs/getting-started.md) | Installation, requirements, first query |
| [Pagination Patterns](docs/patterns.md) | First, last, previous, next page patterns |
| [API Reference](docs/api-reference.md) | Full API details, including cursor, executor, and sort-registry APIs |
| [Prebuilt Definitions](docs/prebuilt-definitions.md) | Caching pagination definitions for performance |
| [Database Indexing](docs/indexing.md) | Composite indexes, deterministic definitions |
| [NULL Handling](docs/null-handling.md) | Computed columns, expression coalescing |
| [Loose Typing](docs/loose-typing.md) | DTOs, projections, anonymous type references |
| [Analyzers & Diagnostics](docs/diagnostics.md) | Build-time warnings and fixes |

## Samples

The [samples](samples) directory contains a Razor Pages demo with four pagination variations and a Minimal API endpoint showcasing loose typing with opaque cursor tokens.

## License

[MIT](LICENSE)
