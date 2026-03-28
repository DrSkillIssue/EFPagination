# Entity Framework Pagination (EFPagination)

Keyset pagination for Entity Framework Core. Also known as seek or cursor pagination.

Keyset pagination delivers **stable query performance regardless of page depth**, unlike offset pagination (`Skip`/`Take`) which degrades linearly as you skip more rows.

## Installation

```
dotnet add package EFPagination
```

For ASP.NET Core integration (optional):

```
dotnet add package EFPagination.AspNetCore
```

Requires .NET 10 and EF Core 10.

## Quick Start

```cs
using EFPagination;

// Build once and store as a static readonly field.
static readonly PaginationQueryDefinition<User> Definition =
    PaginationQuery.Build<User>(b => b.Descending(x => x.Created).Ascending(x => x.Id));

// First page.
var page = await dbContext.Users
    .Keyset(Definition)
    .TakeAsync(20);

// Next page -- pass the cursor from the previous response.
var nextPage = await dbContext.Users
    .Keyset(Definition)
    .After(page.NextCursor!)
    .TakeAsync(20);

// Previous page.
var prevPage = await dbContext.Users
    .Keyset(Definition)
    .Before(nextPage.PreviousCursor!)
    .TakeAsync(20);
```

## What's Included

- Fluent keyset pagination via `.Keyset(definition).After(cursor).TakeAsync(20)`
- `CursorPage<T>` with opaque `NextCursor`/`PreviousCursor` tokens
- `IAsyncEnumerable` streaming via `.Keyset(definition).StreamAsync(100)`
- Prebuilt definitions with cached expression tree templates for zero per-request overhead
- `SortField.Create<T>` one-line sort field factory
- `PaginationSortRegistry<T>` for request-driven dynamic sorting
- Opaque cursor encoding/decoding with schema fingerprinting and optional HMAC signing
- `PaginationQuery.Build<T>(string, ...)` for runtime sort definitions
- Low-level `Paginate()` API for custom `IQueryable` composition
- ASP.NET Core integration: `PaginationRequest`, `PaginatedResponse<T>`, `FromRequest`, `ToPaginatedResponse`
- Roslyn analyzers: nullable columns (KP0001), non-unique tiebreakers (KP0002), ad-hoc builders in hot paths (KP0003), missing order correction (KP0004)

## API

### Keyset (Fluent API)

The primary API surface. Chain `.Keyset()` on any `IQueryable<T>`:

```cs
// First page
var page = await db.Users
    .Keyset(definition)
    .TakeAsync(20);

// Forward from cursor
var page = await db.Users
    .Keyset(definition)
    .After(cursor)
    .TakeAsync(20);

// Backward from cursor
var page = await db.Users
    .Keyset(definition)
    .Before(cursor)
    .TakeAsync(20);

// With total count
var page = await db.Users
    .Keyset(definition)
    .After(cursor)
    .IncludeCount()
    .TakeAsync(20);

// With max page size clamp (defaults to 500)
var page = await db.Users
    .Keyset(definition)
    .After(cursor)
    .MaxPageSize(100)
    .TakeAsync(requestedPageSize);

// With entity reference instead of cursor
var page = await db.Users
    .Keyset(definition)
    .AfterEntity(lastUser)
    .TakeAsync(20);

// Stream all pages
await foreach (var batch in db.Users.Keyset(definition).StreamAsync(100))
{
    // process batch
}
```

`TakeAsync` returns `CursorPage<T>`:

| Property | Type | Description |
|----------|------|-------------|
| `Items` | `List<T>` | The page items in correct order. |
| `NextCursor` | `string?` | Opaque cursor for the next page, or `null` when no more pages. |
| `PreviousCursor` | `string?` | Opaque cursor for the previous page, or `null` on the first page. |
| `TotalCount` | `int` | Total rows when `IncludeCount()` was called; otherwise `-1`. |

### PaginationQueryDefinition\<T\>

A prebuilt, reusable pagination definition. Build once, reuse across requests:

```cs
static readonly PaginationQueryDefinition<User> Definition =
    PaginationQuery.Build<User>(b => b.Descending(x => x.Created).Ascending(x => x.Id));
```

### PaginationQuery

Factory for building reusable definitions:

```cs
// From a builder action (recommended):
PaginationQueryDefinition<T> Build<T>(Action<PaginationBuilder<T>> builderAction)

// From a property name string (for runtime sort fields):
PaginationQueryDefinition<T> Build<T>(
    string propertyName,
    bool descending,
    string? tiebreaker = "Id",
    bool tiebreakerDescending = false)
```

### PaginationBuilder\<T\>

Fluent builder for defining pagination columns, used inside `PaginationQuery.Build<T>()`:

| Method | Description |
|--------|-------------|
| `Ascending<TCol>(Expression<Func<T, TCol>>)` | Adds a column with ascending sort. |
| `Descending<TCol>(Expression<Func<T, TCol>>)` | Adds a column with descending sort. |
| `ConfigureColumn<TCol>(Expression<Func<T, TCol>>, bool isDescending)` | Adds a column with explicit sort direction. |

Nested properties are supported: `b.Ascending(x => x.Details.Created)`.

### PaginationSortRegistry\<T\> / SortField

Map request sort names to prebuilt definitions:

```cs
var sorts = new PaginationSortRegistry<User>(
    defaultDefinition: PaginationQuery.Build<User>(b => b.Descending(x => x.Created).Ascending(x => x.Id)),
    SortField.Create<User>("created", "Created"),
    SortField.Create<User>("name", "Name"));

var definition = sorts.Resolve(sortBy, sortDir);
```

`SortField.Create<T>` builds both ascending and descending definitions from a property name:

```cs
SortField<T> Create<T>(string name, string propertyName, string? tiebreaker = "Id")
```

### Paginate (Low-Level API)

For consumers who need raw `IQueryable` composition (e.g., `.Include()`, `.Select()`, custom materialization):

```cs
var context = db.Users.Paginate(definition, direction, reference);

var users = await context.Query
    .Include(x => x.Details)
    .Take(20)
    .ToListAsync();
```

`PaginationContext<T>` exposes:

| Property | Description |
|----------|-------------|
| `Query` | The filtered and ordered `IQueryable<T>`. |
| `OrderedQuery` | The ordered `IQueryable<T>` without the pagination filter. |
| `Direction` | The `PaginationDirection` used for this call. |

### PaginationCursor

Encode and decode opaque cursor tokens:

```cs
string Encode(ReadOnlySpan<ColumnValue> values, PaginationCursorOptions? options = null);

bool TryDecode<T>(ReadOnlySpan<char> encoded, PaginationQueryDefinition<T> definition, out PaginationValues<T> values, out int written)
```

`PaginationCursorOptions` supports `SchemaFingerprint` (stale cursor rejection) and `SigningKey` (HMAC verification).

### PaginationDirection

```cs
enum PaginationDirection { Forward, Backward }
```

### IncompatibleReferenceException

Thrown when a reference object is missing a property required by the pagination definition.

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

### Fluent Integration

Use `FromRequest` to apply cursors from a request, or `Keyset(registry, request)` for dynamic sorting:

```cs
// Fixed definition
var page = await db.Users
    .Keyset(definition)
    .FromRequest(request)
    .MaxPageSize(100)
    .TakeAsync(request.PageSize);

return page.ToPaginatedResponse(u => new UserDto(u.Id, u.Name));

// Dynamic sorting via registry
var page = await db.Users
    .Keyset(sortRegistry, request)
    .MaxPageSize(100)
    .TakeAsync(request.PageSize);

return page.ToPaginatedResponse(u => new UserDto(u.Id, u.Name));
```

### PaginatedResponse\<T\>

JSON-serializable response envelope:

```cs
public sealed record PaginatedResponse<T>(
    IReadOnlyList<T> Items,
    string? NextCursor,
    string? PreviousCursor,
    int? TotalCount);
```

### ToPaginatedResponse

Convert a `CursorPage<T>` to a `PaginatedResponse<T>`:

```cs
PaginatedResponse<T> ToPaginatedResponse<T>(this CursorPage<T> page);
PaginatedResponse<TOut> ToPaginatedResponse<T, TOut>(this CursorPage<T> page, Func<T, TOut> selector)
```

## Documentation

| Topic | Description |
|-------|-------------|
| [Getting Started](docs/getting-started.md) | Installation, requirements, first query |
| [Pagination Patterns](docs/patterns.md) | First, last, previous, next page patterns |
| [API Reference](docs/api-reference.md) | Full API details |
| [Prebuilt Definitions](docs/prebuilt-definitions.md) | Caching pagination definitions for performance |
| [Database Indexing](docs/indexing.md) | Composite indexes, deterministic definitions |
| [NULL Handling](docs/null-handling.md) | Computed columns, expression coalescing |
| [Loose Typing](docs/loose-typing.md) | DTOs, projections, anonymous type references |
| [Analyzers & Diagnostics](docs/diagnostics.md) | Build-time warnings and fixes |

## Samples

The [samples](samples) directory contains a Razor Pages demo with four pagination variations and Minimal API endpoints showcasing cursor-based pagination with sort registries.

## License

[MIT](LICENSE)
