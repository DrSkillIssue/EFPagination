# Getting Started

## Requirements

- .NET 10 or later
- EF Core 10 or later

## Installation

```
dotnet add package EFPagination
```

For ASP.NET Core integration (optional):

```
dotnet add package EFPagination.AspNetCore
```

## Your First Query

Add the `using` directive:

```cs
using EFPagination;
```

Define a pagination definition on your entity. A definition specifies which columns to sort by and in what direction. Store it as a `static` field for best performance (see [Prebuilt Definitions](prebuilt-definitions.md)):

```cs
private static readonly PaginationQueryDefinition<User> UserDefinition =
    PaginationQuery.Build<User>(b => b.Descending(x => x.Created).Ascending(x => x.Id));
```

Query the first page:

```cs
var page = await dbContext.Users
    .Keyset(UserDefinition)
    .TakeAsync(20);

// page.Items          -- the 20 (or fewer) items
// page.NextCursor     -- opaque token for the next page, or null when no more
// page.PreviousCursor -- null on the first page
// page.TotalCount     -- -1 (not requested)
```

Query the next page by passing the cursor from the previous response:

```cs
var nextPage = await dbContext.Users
    .Keyset(UserDefinition)
    .After(page.NextCursor!)
    .TakeAsync(20);
```

Query the previous page:

```cs
var prevPage = await dbContext.Users
    .Keyset(UserDefinition)
    .Before(nextPage.PreviousCursor!)
    .TakeAsync(20);
```

## Including Total Count

Pass `.IncludeCount()` to execute an additional `COUNT(*)` query:

```cs
var page = await dbContext.Users
    .Keyset(UserDefinition)
    .IncludeCount()
    .TakeAsync(20);

// page.TotalCount -- total number of rows in the source query
```

## Clamping Page Size

Use `.MaxPageSize()` to set an upper bound on the page size. Requests exceeding this value are clamped. The default max is 500:

```cs
var page = await dbContext.Users
    .Keyset(UserDefinition)
    .After(cursor)
    .MaxPageSize(100)
    .TakeAsync(requestedPageSize);
```

## Streaming All Pages

To process all pages sequentially (data exports, background jobs):

```cs
await foreach (var batch in dbContext.Users.Keyset(UserDefinition).StreamAsync(500))
{
    foreach (var user in batch)
        ProcessUser(user);
}
```

## ASP.NET Core Endpoint

With the `EFPagination.AspNetCore` package, bind pagination parameters from the query string and return a JSON-serializable response:

```cs
using EFPagination;
using EFPagination.AspNetCore;

private static readonly PaginationQueryDefinition<User> Definition =
    PaginationQuery.Build<User>(b => b.Descending(x => x.Created).Ascending(x => x.Id));

app.MapGet("/api/users", async (AppDbContext db, [AsParameters] PaginationRequest request) =>
{
    var page = await db.Users
        .Keyset(Definition)
        .FromRequest(request)
        .MaxPageSize(100)
        .TakeAsync(request.PageSize);

    return page.ToPaginatedResponse(u => new UserDto(u.Id, u.Name, u.Created));
});
```

`PaginationRequest` binds `After`, `Before`, `PageSize`, `SortBy`, and `SortDir` from the query string. `ToPaginatedResponse` converts the `CursorPage<T>` to a `PaginatedResponse<TOut>` with `Items`, `NextCursor`, `PreviousCursor`, and `TotalCount`.

For dynamic sorting via a registry:

```cs
private static readonly PaginationSortRegistry<User> Sorts = new(
    defaultDefinition: Definition,
    SortField.Create<User>("created", "Created"),
    SortField.Create<User>("name", "Name"));

app.MapGet("/api/users", async (AppDbContext db, [AsParameters] PaginationRequest request) =>
{
    var page = await db.Users
        .Keyset(Sorts, request)
        .MaxPageSize(100)
        .TakeAsync(request.PageSize);

    return page.ToPaginatedResponse(u => new UserDto(u.Id, u.Name, u.Created));
});
```

## Next Steps

- [Pagination Patterns](patterns.md) -- All navigation patterns (first, last, previous, next) and streaming
- [Prebuilt Definitions](prebuilt-definitions.md) -- Why and how to cache pagination definitions
- [API Reference](api-reference.md) -- Full API surface including cursor encoding, sort registries, low-level Paginate, and ASP.NET Core helpers
- [Database Indexing](indexing.md) -- Create compatible indexes for optimal performance
