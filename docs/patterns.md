# Pagination Patterns

`Paginate` supports four navigation patterns. All examples assume a pagination definition like:

```cs
private static readonly PaginationQueryDefinition<User> Definition =
    PaginationQuery.Build<User>(b => b.Descending(x => x.Created).Ascending(x => x.Id));
```

## First Page

Omit direction and reference to get the first page:

```cs
var context = dbContext.Users.Paginate(Definition);
```

This is equivalent to:

```cs
var context = dbContext.Users.Paginate(
    Definition,
    PaginationDirection.Forward,
    null);
```

## Last Page

Use `Backward` direction without a reference:

```cs
var context = dbContext.Users.Paginate(
    Definition,
    PaginationDirection.Backward);
```

## Next Page

Pass the **last item** of the current page as the reference with `Forward` direction:

```cs
var context = dbContext.Users.Paginate(
    Definition,
    PaginationDirection.Forward,
    currentPage[^1]);
```

## Previous Page

Pass the **first item** of the current page as the reference with `Backward` direction:

```cs
var context = dbContext.Users.Paginate(
    Definition,
    PaginationDirection.Backward,
    currentPage[0]);
```

> [!WARNING]
> Walking `Backward` returns results in reverse order. Always call `context.EnsureCorrectOrder(data)` after fetching to restore the expected order.

## Complete Example

```cs
private static readonly PaginationQueryDefinition<User> Definition =
    PaginationQuery.Build<User>(b => b.Descending(x => x.Created).Ascending(x => x.Id));

public async Task<PagedResult> GetUsersAsync(int? afterId, int? beforeId)
{
    var query = dbContext.Users.AsQueryable();
    var totalCount = await query.CountAsync();

    var direction = beforeId is not null
        ? PaginationDirection.Backward
        : PaginationDirection.Forward;

    var referenceId = afterId ?? beforeId;
    var reference = referenceId is not null
        ? await dbContext.Users.FindAsync(referenceId)
        : null;

    var context = query.Paginate(Definition, direction, reference);

    var users = await context.Query
        .Take(20)
        .ToListAsync();

    context.EnsureCorrectOrder(users);

    return new PagedResult
    {
        Data = users,
        TotalCount = totalCount,
        HasPrevious = await context.HasPreviousAsync(users),
        HasNext = await context.HasNextAsync(users),
    };
}
```

## See Also

- [API Reference](api-reference.md) — Full method signatures and return types
- [Loose Typing](loose-typing.md) — Use DTOs or anonymous types as references
