# Getting Started

## Requirements

- .NET 10 or later
- EF Core 10 or later

## Installation

```
dotnet add package EFPagination
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
var context = dbContext.Users.Paginate(UserDefinition);

var users = await context.Query
    .Take(20)
    .ToListAsync();

// Always call this — it fixes ordering when paginating backward.
context.EnsureCorrectOrder(users);
```

Query the next page by passing the last item as a reference:

```cs
var nextContext = dbContext.Users.Paginate(
    UserDefinition,
    PaginationDirection.Forward,
    users[^1]);

var nextUsers = await nextContext.Query
    .Take(20)
    .ToListAsync();

nextContext.EnsureCorrectOrder(nextUsers);
```

Check if there are more pages:

```cs
var hasPrevious = await context.HasPreviousAsync(users);
var hasNext = await context.HasNextAsync(users);
```

`HasPreviousAsync`/`HasNextAsync` are useful for rendering Previous/Next buttons in your UI.

## Next Steps

- [Pagination Patterns](patterns.md) — All four navigation patterns (first, last, previous, next)
- [Prebuilt Definitions](prebuilt-definitions.md) — Why and how to cache pagination definitions
- [API Reference](api-reference.md#paginationcursor) — Cursor encoding/decoding, sort registries, and executor helpers
- [Database Indexing](indexing.md) — Create compatible indexes for optimal performance

## Cursor-Based API Example

For HTTP APIs, use `PaginationExecutor` and `PaginationCursor` to return page metadata and a resumable cursor in one flow:

```cs
private static readonly PaginationQueryDefinition<User> UserDefinition =
    PaginationQuery.Build<User>(b => b.Descending(x => x.Created).Ascending(x => x.Id));

public async Task<object> GetUsersAsync(string? after)
{
    ColumnValue[] cursorValues =
    [
        new(nameof(User.Created), null),
        new(nameof(User.Id), null),
    ];

    object? reference = null;
    int? totalCountFromCursor = null;

    if (!string.IsNullOrWhiteSpace(after) &&
        PaginationCursor.TryDecode(after, cursorValues, out _, out _, out var totalCount))
    {
        reference = new
        {
            Created = (DateTime)cursorValues[0].Value!,
            Id = (int)cursorValues[1].Value!,
        };
        totalCountFromCursor = totalCount;
    }

    var page = await PaginationExecutor.ExecuteAsync(
        dbContext.Users,
        UserDefinition,
        pageSize: 20,
        reference,
        includeCount: reference is null);

    string? next = null;
    if (page.Items.Count > 0 && page.HasMore)
    {
        var last = page.Items[^1];
        next = PaginationCursor.Encode(
        [
            new(nameof(User.Created), last.Created),
            new(nameof(User.Id), last.Id),
        ],
        new PaginationCursorOptions(
            TotalCount: page.TotalCount >= 0 ? page.TotalCount : totalCountFromCursor));
    }

    return new
    {
        Items = page.Items,
        page.HasMore,
        TotalCount = page.TotalCount >= 0 ? page.TotalCount : totalCountFromCursor,
        NextCursor = next,
    };
}
```
