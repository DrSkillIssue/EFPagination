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

## See Also

- [API Reference](api-reference.md#paginationquerydefinitiont) — `PaginationQueryDefinition<T>` details
- [Getting Started](getting-started.md) — End-to-end example with prebuilt definition
