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

// Next page — pass the last item as a cursor.
var nextContext = dbContext.Users.Paginate(definition, PaginationDirection.Forward, users[^1]);
var nextUsers = await nextContext.Query.Take(20).ToListAsync();
nextContext.EnsureCorrectOrder(nextUsers);

// Check boundaries.
var hasPrevious = await nextContext.HasPreviousAsync(nextUsers);
var hasNext = await nextContext.HasNextAsync(nextUsers);
```

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

### PaginationBuilder\<T\>

Fluent builder for defining pagination columns, used inside `PaginationQuery.Build<T>()`:

| Method | Description |
|--------|-------------|
| `Ascending<TCol>(Expression<Func<T, TCol>>)` | Adds a column with ascending sort. |
| `Descending<TCol>(Expression<Func<T, TCol>>)` | Adds a column with descending sort. |
| `ConfigureColumn<TCol>(Expression<Func<T, TCol>>, bool isDescending)` | Adds a column with explicit sort direction. Useful when the direction is dynamic. |

Nested properties are supported: `b.Ascending(x => x.Details.Created)`.

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
| [API Reference](docs/api-reference.md) | Full API details, nested properties, exceptions |
| [Prebuilt Definitions](docs/prebuilt-definitions.md) | Caching pagination definitions for performance |
| [Database Indexing](docs/indexing.md) | Composite indexes, deterministic definitions |
| [NULL Handling](docs/null-handling.md) | Computed columns, expression coalescing |
| [Loose Typing](docs/loose-typing.md) | DTOs, projections, anonymous type references |
| [Analyzers & Diagnostics](docs/diagnostics.md) | Build-time warnings and fixes |

## Samples

The [samples](samples) directory contains a Razor Pages demo with four pagination variations and a Minimal API endpoint showcasing loose typing with JSON cursor tokens.

## License

[MIT](LICENSE)
