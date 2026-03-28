# Prebuilt Definitions

## Why Prebuild?

A prebuilt `PaginationQueryDefinition<T>` does reflection, expression tree building, and cache setup once. Per-call overhead is reduced to near zero because the internal expression templates are cached and reused.

## Creating a Definition

Use `PaginationQuery.Build<T>()` and store the result as a `static readonly` field:

```cs
private static readonly PaginationQueryDefinition<User> UserDefinition =
    PaginationQuery.Build<User>(b => b.Descending(x => x.Created).Ascending(x => x.Id));
```

You can also build a reusable definition from runtime property names:

```cs
var createdDescending = PaginationQuery.Build<User>(
    propertyName: "Created",
    descending: true,
    tiebreaker: "Id",
    tiebreakerDescending: false);
```

The string overload caches by `(propertyName, descending, tiebreaker, tiebreakerDescending)` per entity type, so repeated calls with the same inputs reuse the same definition instance. The cache is bounded to 256 entries per type.

## Using a Definition

Pass it to `Keyset()`:

```cs
var page = await dbContext.Users
    .Keyset(UserDefinition)
    .After(cursor)
    .TakeAsync(20);
```

## When to Use Each

| Approach | Use when |
|----------|----------|
| Lambda `Build<T>(b => ...)` | Known sort orders, stored as `static readonly` fields |
| String `Build<T>(string, ...)` | User-selectable sort fields, admin grids, runtime sort configuration |
| `SortField.Create<T>` + `PaginationSortRegistry<T>` | Multiple sort fields exposed to API consumers |

## Sort Registries

If callers can choose among several sort fields, register them once and resolve the right definition per request:

```cs
private static readonly PaginationSortRegistry<User> Sorts = new(
    defaultDefinition: PaginationQuery.Build<User>(b => b.Descending(x => x.Created).Ascending(x => x.Id)),
    SortField.Create<User>("created", "Created"),
    SortField.Create<User>("name", "Name"));
```

### With the Fluent API

The `Keyset(registry, request)` overload resolves the definition, applies cursors, and embeds the sort key in cursor tokens:

```cs
var page = await dbContext.Users
    .Keyset(Sorts, request)
    .MaxPageSize(100)
    .TakeAsync(request.PageSize);
```

### Manual Resolution

You can also resolve manually and pass the definition to `Keyset()`:

```cs
var definition = Sorts.Resolve(sortBy, sortDir);
var page = await dbContext.Users
    .Keyset(definition)
    .After(cursor)
    .TakeAsync(20);
```

`Resolve` is case-insensitive for both field names and `desc`, and falls back to the default definition when the request asks for an unknown sort.

`TryResolve` returns `false` instead of falling back, allowing you to reject invalid sort field names:

```cs
if (!Sorts.TryResolve(sortBy, sortDir, out var definition))
    return Results.BadRequest("Invalid sort field.");
```

## See Also

- [API Reference](api-reference.md#paginationquerydefinitiont) -- `PaginationQueryDefinition<T>` details
- [Getting Started](getting-started.md) -- End-to-end example with prebuilt definition
