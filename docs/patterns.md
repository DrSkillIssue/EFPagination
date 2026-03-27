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

## Cursor Token Pattern

For API responses, encode the last row's keyset values into a cursor token instead of exposing raw query parameters:

```cs
private static readonly PaginationQueryDefinition<User> Definition =
    PaginationQuery.Build<User>(b => b.Descending(x => x.Created).Ascending(x => x.Id));

public async Task<object> GetUsersAsync(string? after)
{
    object? reference = null;
    int? totalCountFromCursor = null;
    ColumnValue[] values =
    [
        new(nameof(User.Created), null),
        new(nameof(User.Id), null),
    ];

    if (!string.IsNullOrWhiteSpace(after) &&
        PaginationCursor.TryDecode(after, values, out _, out _, out var totalCount))
    {
        reference = new
        {
            Created = (DateTime)values[0].Value!,
            Id = (int)values[1].Value!,
        };
        totalCountFromCursor = totalCount;
    }

    var page = await PaginationExecutor.ExecuteAsync(
        dbContext.Users,
        Definition,
        pageSize: 20,
        reference,
        includeCount: reference is null);

    string? nextCursor = null;
    if (page.HasMore)
    {
        var last = page.Items[^1];
        nextCursor = PaginationCursor.Encode(
        [
            new(nameof(User.Created), last.Created),
            new(nameof(User.Id), last.Id),
        ],
        new PaginationCursorOptions(TotalCount: page.TotalCount >= 0 ? page.TotalCount : totalCountFromCursor));
    }

    return new { page.Items, page.HasMore, NextCursor = nextCursor };
}
```

This pattern keeps the HTTP contract opaque while still preserving type-safe keyset values, sort metadata, and optional total-count metadata.

## See Also

- [API Reference](api-reference.md) — Full method signatures and return types
- [Loose Typing](loose-typing.md) — Use DTOs or anonymous types as references
