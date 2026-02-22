# Loose Typing

The reference objects and data lists accepted by `Paginate`, `HasPreviousAsync`, `HasNextAsync`, and `EnsureCorrectOrder` are **loosely typed**. They don't need to be the entity type — they just need properties with names matching the pagination columns.

This enables common patterns like:

- Using DTOs or anonymous types as references (no entity load required)
- Calling `HasNextAsync` on projected results
- Building references from API query parameters

## Example: Anonymous Type Reference

```cs
// Build a reference from known values — no database query needed.
var reference = new { Created = someDate, Id = someId };

var context = dbContext.Users.Paginate(
    b => b.Descending(x => x.Created).Ascending(x => x.Id),
    PaginationDirection.Forward,
    reference);
```

## Example: Minimal API with DTOs

The most common real-world pattern — receive cursor values as query parameters, paginate, project to DTOs:

```cs
app.MapGet("/api/users", async (AppDbContext db, DateTime? afterCreated, int? afterId) =>
{
    // Build reference from query params — no entity load required.
    object? reference = (afterCreated, afterId) switch
    {
        (not null, not null) => new { Created = afterCreated.Value, Id = afterId.Value },
        _ => null,
    };

    var context = db.Users.Paginate(Definition, PaginationDirection.Forward, reference);

    var users = await context.Query
        .Take(20)
        .Select(u => new UserDto(u.Id, u.Name, u.Created))
        .ToListAsync();

    context.EnsureCorrectOrder(users);

    // HasNextAsync works with projected DTOs, not just entities.
    var hasNext = await context.HasNextAsync(users);

    return Results.Ok(new { Data = users, HasNext = hasNext });
});
```

See the [sample API endpoint](../samples/Endpoints/UsersApi.cs) for a complete working example.

## Nested Properties

When the definition uses nested properties, the reference must have matching nested structure:

```cs
// Definition accesses x.Details.Created
var definition = PaginationQuery.Build<User>(b => b.Ascending(x => x.Details.Created));

// Reference must also have a Details.Created property:
var reference = new
{
    Details = new
    {
        Created = someDate,
    },
};

var context = dbContext.Users.Paginate(definition, direction, reference);
```

## Requirements

- Property names must match the pagination column names **exactly** (case-sensitive).
- Property types must be compatible (same type or implicitly convertible).
- Missing properties throw `IncompatibleReferenceException` with details about which property was expected and on which types.

## See Also

- [API Reference](api-reference.md#exceptions) — `IncompatibleReferenceException` details
- [Patterns](patterns.md) — Standard pagination patterns
