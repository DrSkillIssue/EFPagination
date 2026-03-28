# Pagination Patterns

All examples assume a pagination definition like:

```cs
private static readonly PaginationQueryDefinition<User> Definition =
    PaginationQuery.Build<User>(b => b.Descending(x => x.Created).Ascending(x => x.Id));
```

## First Page

Call `Keyset()` without a cursor to get the first page:

```cs
var page = await dbContext.Users
    .Keyset(Definition)
    .TakeAsync(20);
```

## Next Page

Pass `NextCursor` from the previous response:

```cs
var nextPage = await dbContext.Users
    .Keyset(Definition)
    .After(page.NextCursor!)
    .TakeAsync(20);
```

When `NextCursor` is `null`, there are no more pages.

## Previous Page

Pass `PreviousCursor` from the current response:

```cs
var prevPage = await dbContext.Users
    .Keyset(Definition)
    .Before(page.PreviousCursor!)
    .TakeAsync(20);
```

When `PreviousCursor` is `null`, you are on the first page.

## Last Page

Use `BeforeEntity` with a sentinel value that sorts after all real data. For example, if the definition sorts by `Created DESC, Id ASC`:

```cs
var lastPage = await dbContext.Users
    .Keyset(Definition)
    .BeforeEntity(new { Created = DateTime.MinValue, Id = int.MaxValue })
    .TakeAsync(20);
```

The sentinel values depend on your sort direction. For descending columns, use the minimum possible value. For ascending columns, use the maximum possible value.

## Entity Reference

If you have the entity in memory, use `AfterEntity`/`BeforeEntity` instead of a cursor string:

```cs
var page = await dbContext.Users
    .Keyset(Definition)
    .AfterEntity(lastUser)
    .TakeAsync(20);
```

The entity must have properties matching the pagination definition columns. Anonymous types work:

```cs
var page = await dbContext.Users
    .Keyset(Definition)
    .AfterEntity(new { Created = someDate, Id = someId })
    .TakeAsync(20);
```

## Pre-Decoded Values

If you decoded cursor values into a `PaginationValues<T>`, pass them directly:

```cs
if (PaginationCursor.TryDecode(cursorString, Definition, out var values, out _))
{
    var page = await dbContext.Users
        .Keyset(Definition)
        .After(values)
        .TakeAsync(20);
}
```

## Streaming All Pages

To process every row in the table without loading everything into memory:

```cs
await foreach (var batch in dbContext.Users.Keyset(Definition).StreamAsync(500))
{
    foreach (var user in batch)
        ProcessUser(user);
}
```

Streaming only supports forward pagination. You can start from a cursor position:

```cs
await foreach (var batch in dbContext.Users.Keyset(Definition).After(cursor).StreamAsync(500))
{
    // processes all pages after the cursor position
}
```

## Including Total Count

Add `.IncludeCount()` to execute a `COUNT(*)` alongside the page query:

```cs
var page = await dbContext.Users
    .Keyset(Definition)
    .After(cursor)
    .IncludeCount()
    .TakeAsync(20);

// page.TotalCount contains the total row count
```

The total count is embedded in cursor tokens when available, so subsequent pages can carry it forward without re-executing the count query.

## Complete Endpoint Example

```cs
private static readonly PaginationQueryDefinition<User> Definition =
    PaginationQuery.Build<User>(b => b.Descending(x => x.Created).Ascending(x => x.Id));

app.MapGet("/api/users", async (
    AppDbContext db,
    string? after = null,
    string? before = null,
    int pageSize = 20) =>
{
    var builder = db.Users.Keyset(Definition).MaxPageSize(100).IncludeCount();

    if (before is not null)
        builder = builder.Before(before);
    else if (after is not null)
        builder = builder.After(after);

    var page = await builder.TakeAsync(pageSize);

    return Results.Ok(new
    {
        page.Items,
        page.NextCursor,
        page.PreviousCursor,
        page.TotalCount,
    });
});
```

## Dynamic Sorting with Sort Registry

For endpoints that accept user-specified sort fields:

```cs
private static readonly PaginationSortRegistry<User> Sorts = new(
    defaultDefinition: Definition,
    SortField.Create<User>("created", "Created"),
    SortField.Create<User>("name", "Name"));

app.MapGet("/api/users", async (
    AppDbContext db,
    [AsParameters] PaginationRequest request) =>
{
    var page = await db.Users
        .Keyset(Sorts, request)
        .MaxPageSize(100)
        .TakeAsync(request.PageSize);

    return page.ToPaginatedResponse(u => new UserDto(u.Id, u.Name, u.Created));
});
```

The `Keyset(registry, request)` overload resolves the definition from `request.SortBy`/`request.SortDir`, applies the cursor from `request.After` or `request.Before`, and embeds the sort key in cursor tokens so sort context is preserved across pages.

## Manual Cursor Encode/Decode

If you need custom metadata or HMAC signing on cursors:

```cs
private static readonly byte[] CursorKey = RandomNumberGenerator.GetBytes(32);

// Encode
var cursorToken = PaginationCursor.Encode(
[
    new ColumnValue(nameof(User.Created), lastUser.Created),
    new ColumnValue(nameof(User.Id), lastUser.Id),
],
new PaginationCursorOptions(SigningKey: CursorKey));

// Decode
if (PaginationCursor.TryDecode(cursorToken, Definition, out var values, out _))
{
    var page = await dbContext.Users
        .Keyset(Definition)
        .After(values)
        .TakeAsync(20);
}
```

## See Also

- [API Reference](api-reference.md) -- Full method signatures and return types
- [Loose Typing](loose-typing.md) -- Use DTOs or anonymous types as entity references
- [Prebuilt Definitions](prebuilt-definitions.md) -- Sort registries and definition caching
