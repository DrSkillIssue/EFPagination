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

## PaginationContext\<T\>

Returned by `Paginate`. Provides access to the filtered query and navigation helpers.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Query` | `IQueryable<T>` | The filtered and ordered query. Chain `.Take()`, `.Select()`, etc. |
| `Direction` | `PaginationDirection` | The direction used for this pagination call. |

### Methods

#### EnsureCorrectOrder

```cs
void EnsureCorrectOrder<TData>(List<TData> data)
```

Reverses the list in-place when the direction is `Backward`. Always call this after fetching data.

#### HasPreviousAsync / HasNextAsync

```cs
Task<bool> HasPreviousAsync<TData>(List<TData> data, CancellationToken ct = default)
Task<bool> HasNextAsync<TData>(List<TData> data, CancellationToken ct = default)
```

Checks whether there is more data before/after the current page. The data list can be of any type with matching pagination column properties (see [Loose Typing](loose-typing.md)).

## PaginationQueryDefinition\<T\>

A prebuilt, reusable pagination definition. Created via `PaginationQuery.Build<T>()`.

```cs
static readonly PaginationQueryDefinition<User> Definition =
    PaginationQuery.Build<User>(b => b.Descending(x => x.Created).Ascending(x => x.Id));
```

See [Prebuilt Definitions](prebuilt-definitions.md) for details.

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
