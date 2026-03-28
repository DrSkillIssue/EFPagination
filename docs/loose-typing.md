# Loose Typing

## Fluent API

The fluent `Keyset()` API handles cursor encoding and decoding internally, so loose typing is rarely needed. Cursor strings are opaque tokens that carry typed values -- no manual reference construction required.

However, `AfterEntity` and `BeforeEntity` accept **any object** with properties matching the pagination definition columns. This enables patterns like:

```cs
// Anonymous type -- no entity load required:
var page = await dbContext.Users
    .Keyset(Definition)
    .AfterEntity(new { Created = someDate, Id = someId })
    .TakeAsync(20);
```

## Low-Level API

The `Paginate`, `HasPreviousAsync`, `HasNextAsync`, and `EnsureCorrectOrder` extension methods are **loosely typed**. Reference objects and data lists don't need to be the entity type -- they just need properties with names matching the pagination columns.

This enables common patterns like:

- Using DTOs or anonymous types as references (no entity load required)
- Calling `HasNextAsync` on projected results
- Building references from API query parameters

### Example: Anonymous Type Reference

```cs
var reference = new { Created = someDate, Id = someId };

var context = dbContext.Users.Paginate(Definition, PaginationDirection.Forward, reference);

var users = await context.Query
    .Take(20)
    .ToListAsync();

context.EnsureCorrectOrder(users);
```

### Example: HasNextAsync on Projected DTOs

```cs
var context = dbContext.Users.Paginate(Definition, PaginationDirection.Forward, reference);

var users = await context.Query
    .Take(20)
    .Select(u => new UserDto(u.Id, u.Name, u.Created))
    .ToListAsync();

context.EnsureCorrectOrder(users);

// HasNextAsync works with projected DTOs, not just entities.
var hasNext = await context.HasNextAsync(users);
```

## Nested Properties

When the definition uses nested properties, the reference must have matching nested structure:

```cs
// Definition accesses x.Details.Created
var definition = PaginationQuery.Build<User>(b => b.Ascending(x => x.Details.Created).Ascending(x => x.Id));

// Reference must also have a Details.Created property:
var reference = new
{
    Details = new
    {
        Created = someDate,
    },
    Id = someId,
};

var page = await dbContext.Users
    .Keyset(definition)
    .AfterEntity(reference)
    .TakeAsync(20);
```

## Requirements

- Property names must match the pagination column names **exactly** (case-sensitive).
- Property types must be compatible (same type or implicitly convertible).
- Missing properties throw `IncompatibleReferenceException` with details about which property was expected and on which types.

## See Also

- [API Reference](api-reference.md#exceptions) -- `IncompatibleReferenceException` details
- [Patterns](patterns.md) -- Standard pagination patterns
