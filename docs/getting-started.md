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
- [Database Indexing](indexing.md) — Create compatible indexes for optimal performance
