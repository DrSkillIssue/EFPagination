# Prebuilt Definitions

## Why Prebuild?

When you pass a builder action directly to `Paginate`, the library must construct the pagination definition on every call. This includes reflection, expression tree building, and cache setup.

A prebuilt `PaginationQueryDefinition<T>` does this work once and caches the internal expression templates, reducing per-call overhead to near zero.

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

The string overload caches by `(T, propertyName, descending, tiebreaker, tiebreakerDescending)`, so repeated calls with the same inputs reuse the same definition instance.

## Using a Definition

Pass it to `Paginate` instead of a builder action:

```cs
// With prebuilt definition (recommended):
var context = dbContext.Users.Paginate(UserDefinition, direction, reference);

// Without prebuilt definition (avoid in hot paths):
var context = dbContext.Users.Paginate(
    b => b.Descending(x => x.Created).Ascending(x => x.Id),
    direction,
    reference);
```

## When to Use Each

| Approach | Use when |
|----------|----------|
| Prebuilt `PaginationQueryDefinition<T>` | Production code, API endpoints, repeated queries |
| Inline builder action | Prototyping, tests, one-off queries |
| String-based `Build<T>(...)` | User-selectable sort fields, admin grids, runtime sort configuration |

## Sort Registries

If callers can choose among several sort fields, register them once and resolve the right definition per request:

```cs
private static readonly PaginationSortRegistry<User> Sorts = new(
    defaultDefinition: PaginationQuery.Build<User>(b => b.Descending(x => x.Created).Ascending(x => x.Id)),
    new SortField<User>(
        "created",
        PaginationQuery.Build<User>("Created", descending: false, tiebreaker: "Id"),
        PaginationQuery.Build<User>("Created", descending: true, tiebreaker: "Id")),
    new SortField<User>(
        "name",
        PaginationQuery.Build<User>("Name", descending: false, tiebreaker: "Id"),
        PaginationQuery.Build<User>("Name", descending: true, tiebreaker: "Id")));

var definition = Sorts.Resolve(sortBy, sortDir);
var context = dbContext.Users.Paginate(definition, reference: cursorReference);
```

`Resolve` is case-insensitive for both field names and `desc`, and falls back to the default definition when the request asks for an unknown sort.

## See Also

- [API Reference](api-reference.md#paginationquerydefinitiont) — `PaginationQueryDefinition<T>` details
- [Getting Started](getting-started.md) — End-to-end example with prebuilt definition
