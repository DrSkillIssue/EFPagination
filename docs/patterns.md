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
if (PaginationCursor.TryDecode(cursorString, Definition, out var values, out var metadata))
{
    var page = await dbContext.Users
        .Keyset(Definition)
        .After(values)
        .TakeAsync(20);
}
```

The `metadata` (`CursorMetadata`) exposes any `SortBy`, `TotalCount`, and schema `Fingerprint` that were embedded in the cursor.

## Server-Side Projection

Pass an `Expression<Func<T, TOut>>` to `TakeAsync` or `StreamAsync` to project to a DTO **inside the SQL statement** that applies the keyset `ORDER BY`. The SELECT list materializes only the projected columns plus the keyset key columns; subqueries inside the selector stay server-side.

```cs
public sealed record UserListItem(int Id, string Name, string Email, DateTime Created);

var page = await dbContext.Users
    .Keyset(Definition)
    .After(cursor)
    .TakeAsync(20, u => new UserListItem(u.Id, u.Name, u.Email, u.Created));
```

The returned `CursorPage<UserListItem>` carries cursor tokens just like the entity-typed overload — the keyset key values ride alongside the projection in an internal envelope and feed cursor encoding without ever touching the projected DTO. This means **the DTO does not need property names matching the pagination definition** (see [Loose Typing](loose-typing.md)).

### Server-Side Subqueries

Subqueries embedded in the projection translate as correlated subqueries / `OUTER APPLY` instead of producing N+1 round trips:

```cs
public sealed record AccountListItem(
    Guid Id, string UserName, string[] Roles, DateTime Created);

var page = await dbContext.Accounts
    .Keyset(AccountsDefinition)
    .FromRequest(request)
    .TakeAsync(request.PageSize, a => new AccountListItem(
        a.Id,
        a.UserName,
        dbContext.Set<UserRole>()
            .Where(ur => ur.UserId == a.Id)
            .Join(dbContext.Set<Role>(), ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
            .OrderBy(n => n)
            .ToArray(),
        a.Created));
```

### Streaming with Projection

`StreamAsync<TOut>` yields projected batches end-to-end:

```cs
await foreach (var batch in dbContext.Users.Keyset(Definition)
    .StreamAsync(500, u => new UserListItem(u.Id, u.Name, u.Email, u.Created)))
{
    foreach (var item in batch)
        ProcessItem(item);
}
```

### Limits

The projection path supports 1–8 keyset key columns. Definitions outside that range throw `NotSupportedException` when the projected overload is invoked. The entity-typed `TakeAsync` / `StreamAsync` overloads have no such limit.

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

`COUNT(*)` runs once, on the cursor-less request; the total rides the cursor and is reused on every
later page. A cursor-less request recomputes it.

> **Cursor–query contract.** A cursor belongs to the query that produced it. Reusing one after the
> filter changes still returns a valid, ordered slice, but resumes from the old sort position and
> reports the old count — drop the cursor when the query changes.

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

The `Keyset(registry, request)` overload resolves the definition from `request.SortBy`/`request.SortDir`, applies the cursor from `request.After` or `request.Before`, and embeds the sort key in cursor tokens so sort context is preserved across pages. It throws `BadHttpRequestException` (status 400) for an unknown sort field or an unsupported direction.

## Manual Cursor Encode/Decode

Cursor encoding is schema-bound: both `Encode` and `TryDecode` take a `PaginationQueryDefinition<T>` and infer types from it. Use this when you need custom metadata (logical sort key, total count) or HMAC signing on cursors outside the fluent builder:

```cs
private static readonly byte[] CursorKey = RandomNumberGenerator.GetBytes(32);

// Encode from an entity (or DTO with matching property names):
var cursorToken = PaginationCursor.Encode(
    Definition,
    lastUser,
    new PaginationCursorOptions(SigningKey: CursorKey));

// Decode: returns PaginationValues<T> plus header metadata in one call.
if (PaginationCursor.TryDecode(cursorToken, Definition, out var values, out var meta, CursorKey))
{
    // meta.SortBy / meta.TotalCount / meta.Fingerprint are populated when present in the payload.
    var page = await dbContext.Users
        .Keyset(Definition)
        .After(values)
        .TakeAsync(20);
}
```

Or encode from pre-extracted values when you already have them in column order:

```cs
var values = PaginationValues<User>.Create(Definition, lastUser.Created, lastUser.Id);
var cursorToken = PaginationCursor.Encode(Definition, values, new PaginationCursorOptions(SigningKey: CursorKey));
```

## See Also

- [API Reference](api-reference.md) -- Full method signatures and return types
- [Loose Typing](loose-typing.md) -- Use DTOs or anonymous types as entity references
- [Prebuilt Definitions](prebuilt-definitions.md) -- Sort registries and definition caching
